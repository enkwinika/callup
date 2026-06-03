using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Mvc;
using CallUp.Models;

namespace CallUp.Controllers
{
    [CallUp.Attributes.AuthorizeRole("Provider")]
    public class ProviderController : CallUpController
    {
        private CallUp.Services.IEmailService _emailService = new CallUp.Services.EmailService();
        private CallUp.Services.IAuditService _auditService = new CallUp.Services.AuditService();
        private CallUp.Services.IGeoService _geoService = new CallUp.Services.GeoService();
        private CallUp.Services.ILogService _logService = new CallUp.Services.LogService();

        public ActionResult Index()
        {
            try
            {
                string role = Session["UserRole"]?.ToString();
            if (role != "Provider")
                return RedirectToAction("Login", "Account");

            ViewBag.IsDashboard = true;
            ViewBag.UserRole = role;
            ViewBag.FullName = Session["FullName"]?.ToString();

            // Load open service requests (opportunities to bid on)
            var opportunities = new List<ServiceRequest>();

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                // Load only Open requests. We will filter them in C# for better flexibility with notifications.
                string sql = @"SELECT r.Id, r.Title, r.Description, r.Location, r.Latitude, r.Longitude, r.Status,
                                      r.CreatedAt, r.ServiceDate, r.PriceRange, r.CategoryId, c.Name AS CategoryName, r.ViewCount, (SELECT COUNT(*) FROM Bids WHERE RequestID = r.Id) AS BidCount
                               FROM   ServiceRequests r
                               JOIN   Categories c ON r.CategoryID = c.Id
                               WHERE  r.Status = 'Open'
                               ORDER  BY r.CreatedAt DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                conn.Open();
                SyncExpiredRequests(conn);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var req = new ServiceRequest
                        {
                            Id           = (int)reader["Id"],
                            Title        = reader["Title"]?.ToString() ?? "Untitled",
                            Description  = reader["Description"]?.ToString() ?? "",
                            Location     = reader["Location"]?.ToString() ?? "Unknown",
                            Status       = reader["Status"]?.ToString() ?? "Open",
                            CreatedAt    = (DateTime)reader["CreatedAt"],
                            ServiceDate  = reader["ServiceDate"] as DateTime?,
                            PriceRange   = reader["PriceRange"]?.ToString() ?? "",
                            CategoryName = reader["CategoryName"]?.ToString() ?? "General",
                            ViewCount    = reader["ViewCount"] != DBNull.Value ? (int)reader["ViewCount"] : 0,
                            BidCount     = reader["BidCount"] != DBNull.Value ? (int)reader["BidCount"] : 0,
                            Latitude     = reader["Latitude"] as double?,
                            Longitude    = reader["Longitude"] as double?
                        };
                        opportunities.Add(req);
                    }
                }

                int? currentProvId = Session["UserId"] as int?;

                // Fetch Provider Info (Category and Coordinates)
                int? provCatId = null;
                double? provLat = null, provLon = null;
                if (currentProvId != null)
                {
                    string pInfoSql = "SELECT CategoryId, Latitude, Longitude FROM Users WHERE Id = @PId";
                    SqlCommand pInfoCmd = new SqlCommand(pInfoSql, conn);
                    pInfoCmd.Parameters.AddWithValue("@PId", currentProvId);
                    using (var r = pInfoCmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            provCatId = r["CategoryId"] as int?;
                            provLat = r["Latitude"] as double?;
                            provLon = r["Longitude"] as double?;
                        }
                    }
                }

                // Filter Opportunities: 
                // Show if: Provider was notified OR (matches Category AND Location)
                var filteredOpps = new List<ServiceRequest>();
                foreach (var opp in opportunities)
                {
                    bool wasNotified = false;
                    if (currentProvId != null)
                    {
                        string nSql = "SELECT COUNT(1) FROM Notifications WHERE UserId = @PId AND RequestId = @RId";
                        SqlCommand nCmd = new SqlCommand(nSql, conn);
                        nCmd.Parameters.AddWithValue("@PId", currentProvId);
                        nCmd.Parameters.AddWithValue("@RId", opp.Id);
                        wasNotified = (int)nCmd.ExecuteScalar() > 0;
                    }

                    if (wasNotified)
                    {
                        opp.DistanceInKm = (opp.Latitude != null && opp.Longitude != null && provLat != null && provLon != null) 
                            ? _geoService.CalculateDistance(provLat.Value, provLon.Value, opp.Latitude.Value, opp.Longitude.Value) 
                            : (double?)null;
                        filteredOpps.Add(opp);
                    }
                    else if (provCatId == null || opp.CategoryId == provCatId)
                    {
                        // Filter by proximity (e.g. 50km radius)
                        if (opp.Latitude != null && opp.Longitude != null && provLat != null && provLon != null)
                        {
                            double dist = _geoService.CalculateDistance(provLat.Value, provLon.Value, opp.Latitude.Value, opp.Longitude.Value);
                            opp.DistanceInKm = dist;
                            
                            // Only show if within 50km
                            if (dist <= 50) {
                                filteredOpps.Add(opp);
                            }
                        }
                        else
                        {
                            // If no coordinates, fall back to showing it but without distance (safety)
                            filteredOpps.Add(opp);
                        }
                    }
                }
                opportunities = filteredOpps;

                // Fetch images and competitive bid info for each opportunity
                foreach (var req in opportunities)
                {
                    // 1. Images
                    string imgSql = "SELECT ImagePath FROM RequestImages WHERE RequestId = @ReqId";
                    SqlCommand imgCmd = new SqlCommand(imgSql, conn);
                    imgCmd.Parameters.AddWithValue("@ReqId", req.Id);
                    using (var imgReader = imgCmd.ExecuteReader())
                    {
                        while (imgReader.Read())
                        {
                            req.ImagePaths.Add(imgReader["ImagePath"].ToString());
                        }
                    }

                    // 2. Lowest Bid
                    string lowSql = "SELECT MIN(Amount) FROM Bids WHERE RequestId = @ReqId";
                    SqlCommand lowCmd = new SqlCommand(lowSql, conn);
                    lowCmd.Parameters.AddWithValue("@ReqId", req.Id);
                    var lowVal = lowCmd.ExecuteScalar();
                    if (lowVal != DBNull.Value) req.LowestBid = Convert.ToDecimal(lowVal);

                    // 3. My Current Bid
                    if (currentProvId != null)
                    {
                        string mySql = "SELECT Amount, ETA FROM Bids WHERE RequestId = @ReqId AND ProviderId = @PId";
                        SqlCommand myCmd = new SqlCommand(mySql, conn);
                        myCmd.Parameters.AddWithValue("@ReqId", req.Id);
                        myCmd.Parameters.AddWithValue("@PId", currentProvId);
                        using (var myReader = myCmd.ExecuteReader())
                        {
                            if (myReader.Read())
                            {
                                req.MyBidAmount = Convert.ToDecimal(myReader["Amount"]);
                                req.MyBidEta = myReader["ETA"]?.ToString() ?? "";
                            }
                        }
                    }
                }

                // 4. Get Provider Overall Rating
                if (currentProvId != null)
                {
                    string rInfoSql = "SELECT AVG(CAST(Score AS FLOAT)), COUNT(*) FROM Ratings WHERE ToProviderId = @PId";
                    SqlCommand rInfoCmd = new SqlCommand(rInfoSql, conn);
                    rInfoCmd.Parameters.AddWithValue("@PId", currentProvId);
                    using (var r = rInfoCmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            ViewBag.AvgRating = r[0] != DBNull.Value ? Math.Round(Convert.ToDouble(r[0]), 1).ToString("F1") : (object)null;
                            ViewBag.ReviewCount = r[1].ToString();
                        }
                    }
                }
            }

            // Load Accepted Jobs for this provider
            var myJobs = new List<ServiceRequest>();
            int? provId = Session["UserId"] as int?;
            if (provId != null)
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    // Fetch Active/My Jobs (Work Order issued)
                    string sql = @"SELECT r.Id, r.Title, r.Description, r.Location, r.Status,
                                          r.CreatedAt, r.ServiceDate, c.Name AS CategoryName
                                   FROM   ServiceRequests r
                                   JOIN   Categories c ON r.CategoryID = c.Id
                                   WHERE  r.SelectedProviderId = @ProvId AND r.Status IN ('InProgress', 'PendingConfirmation', 'PendingApproval')
                                   ORDER  BY r.CreatedAt DESC";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ProvId", provId);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            myJobs.Add(new ServiceRequest {
                                Id = (int)reader["Id"],
                                Title = reader["Title"].ToString(),
                                Description = reader["Description"].ToString(),
                                Location = reader["Location"].ToString(),
                                Status = reader["Status"].ToString(),
                                CreatedAt = (DateTime)reader["CreatedAt"],
                                ServiceDate = reader["ServiceDate"] as DateTime?,
                                CategoryName = reader["Name"]?.ToString() ?? reader["CategoryName"].ToString()
                            });
                        }
                    }
                }
            }
            ViewBag.MyJobs = myJobs;

            return View(opportunities);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Provider", "Index", Session["UserId"] as int?);
                TempData["Message"] = "An error occurred while loading your dashboard.";
                TempData["MessageType"] = "error";
                return View(new List<ServiceRequest>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceBid(int requestId, decimal amount, string notes, string eta)
        {
            try
            {
                int? userId = Session["UserId"] as int?;
                if (userId == null) return RedirectToAction("Login", "Account");

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    // 0. Verify Request is still OPEN and not owned by the bidder
                    string checkSql = "SELECT Status, CustomerID FROM ServiceRequests WHERE Id = @Id";
                    SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@Id", requestId);

                    string currentStatus = null;
                    int customerId = 0;
                    using (var r = checkCmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            currentStatus = r["Status"]?.ToString();
                            customerId = (int)r["CustomerID"];
                        }
                    }

                    if (currentStatus != "Open")
                    {
                        TempData["Message"] = "Bidding is closed for this request.";
                        TempData["MessageType"] = "error";
                        return RedirectToAction("Index");
                    }

                    if (customerId == userId)
                    {
                        TempData["Message"] = "You cannot bid on your own service request.";
                        TempData["MessageType"] = "error";
                        return RedirectToAction("Index");
                    }

                    // 0.1 Verify Provider is still VERIFIED
                    string vSql = "SELECT IsVerified FROM Users WHERE Id = @Id";
                    SqlCommand vCmd = new SqlCommand(vSql, conn);
                    vCmd.Parameters.AddWithValue("@Id", userId);
                    bool isVerified = Convert.ToBoolean(vCmd.ExecuteScalar());
                    if (!isVerified)
                    {
                        TempData["Message"] = "Your account must be verified before you can place bids.";
                        TempData["MessageType"] = "error";
                        return RedirectToAction("Index");
                    }


                    // Upsert bid: if exists for this requestId and providerId, UPDATE. Else, INSERT.
                    string upsertSql = @"
                    IF EXISTS (SELECT 1 FROM Bids WHERE RequestId=@R AND ProviderId=@P)
                    BEGIN
                        UPDATE Bids SET Amount=@A, Notes=@N, ETA=@E, CreatedAt=GETDATE() WHERE RequestId=@R AND ProviderId=@P
                    END
                    ELSE
                    BEGIN
                        INSERT INTO Bids (RequestId, ProviderId, Amount, Notes, ETA, Status, CreatedAt) 
                        VALUES (@R, @P, @A, @N, @E, 'Pending', GETDATE())
                    END";

                    var cmd = new SqlCommand(upsertSql, conn);
                    cmd.Parameters.AddWithValue("@R", requestId);
                    cmd.Parameters.AddWithValue("@P", userId);
                    cmd.Parameters.AddWithValue("@A", amount);
                    cmd.Parameters.AddWithValue("@N", string.IsNullOrEmpty(notes) ? (object)DBNull.Value : notes);
                    cmd.Parameters.AddWithValue("@E", string.IsNullOrEmpty(eta) ? (object)DBNull.Value : eta);
                    cmd.ExecuteNonQuery();

                    // Notify Customer
                    string detSql = @"SELECT u.Email, u.FullName, r.Title, p.FullName AS ProvName, p.CompanyName 
                                  FROM ServiceRequests r 
                                  JOIN Users u ON r.CustomerID = u.Id 
                                  JOIN Users p ON p.Id = @PId
                                  WHERE r.Id = @RId";
                    SqlCommand detCmd = new SqlCommand(detSql, conn);
                    detCmd.Parameters.AddWithValue("@RId", requestId);
                    detCmd.Parameters.AddWithValue("@PId", userId);
                    using (var r = detCmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            string custEmail = r["Email"].ToString();
                            string custName = r["FullName"].ToString();
                            string title = r["Title"].ToString();
                            string provName = r["CompanyName"].ToString() != "" ? r["CompanyName"].ToString() : r["ProvName"].ToString();
                            _emailService.NotifyCustomerOfNewBid(custEmail, custName, title, amount, provName);
                        }
                    }

                    _auditService.LogActivity(userId.Value, "Bid Placed", $"Bid placed on request {requestId} for R{amount}", Request.UserHostAddress);

                    TempData["Message"] = "Bid submitted successfully!";
                    TempData["MessageType"] = "success";
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Provider", "PlaceBid", Session["UserId"] as int?);
                TempData["Message"] = "Failed to submit bid.";
                TempData["MessageType"] = "error";
            }
            return RedirectToAction("Provider", "Dashboard");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkAsCompleted(int requestId, string completionNotes, IEnumerable<HttpPostedFileBase> proofImages)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // Update Status to PendingConfirmation, set completion date and notes
                    string updateSql = "UPDATE ServiceRequests SET Status = 'PendingConfirmation', CompletedAt = GETDATE(), CompletionNotes = @Notes WHERE Id = @Id AND SelectedProviderId = @ProvId";
                    SqlCommand cmd = new SqlCommand(updateSql, conn, trans);
                    cmd.Parameters.AddWithValue("@Id", requestId);
                    cmd.Parameters.AddWithValue("@ProvId", userId);
                    cmd.Parameters.AddWithValue("@Notes", completionNotes ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();

                    // --- FINANCIAL PAYOUT INITIALIZATION ---
                    // Fetch the accepted bid amount for this request
                    string bidSql = "SELECT Amount FROM Bids WHERE RequestId = @Id AND ProviderId = @ProvId AND Status IN ('Accepted', 'Selected', 'InProgress')";
                    SqlCommand bidCmd = new SqlCommand(bidSql, conn, trans);
                    bidCmd.Parameters.AddWithValue("@Id", requestId);
                    bidCmd.Parameters.AddWithValue("@ProvId", userId);
                    
                    object bidAmtObj = bidCmd.ExecuteScalar();
                    if (bidAmtObj != null)
                    {
                        decimal totalAmount = (decimal)bidAmtObj;
                        decimal serviceFee = Math.Round(totalAmount * 0.05m, 2);
                        decimal payoutAmount = totalAmount - serviceFee;

                        // Check if payout already exists
                        string checkPayout = "SELECT COUNT(*) FROM Payouts WHERE RequestId = @Id";
                        SqlCommand checkCmd = new SqlCommand(checkPayout, conn, trans);
                        checkCmd.Parameters.AddWithValue("@Id", requestId);
                        if ((int)checkCmd.ExecuteScalar() == 0)
                        {
                            string payoutSql = @"INSERT INTO Payouts (RequestId, ProviderId, TotalAmount, ServiceFee, PayoutAmount, Status, CreatedAt) 
                                               VALUES (@ReqId, @ProvId, @Total, @Fee, @Payout, 'Pending Approval', GETDATE())";
                            SqlCommand payoutCmd = new SqlCommand(payoutSql, conn, trans);
                            payoutCmd.Parameters.AddWithValue("@ReqId", requestId);
                            payoutCmd.Parameters.AddWithValue("@ProvId", userId);
                            payoutCmd.Parameters.AddWithValue("@Total", totalAmount);
                            payoutCmd.Parameters.AddWithValue("@Fee", serviceFee);
                            payoutCmd.Parameters.AddWithValue("@Payout", payoutAmount);
                            payoutCmd.ExecuteNonQuery();
                        }
                    }

                    // Save Proof Images
                    if (proofImages != null)
                    {
                        int count = 0;
                        string uploadDir = Server.MapPath("~/Uploads/Completions/");
                        if (!System.IO.Directory.Exists(uploadDir)) System.IO.Directory.CreateDirectory(uploadDir);

                        foreach (var img in proofImages)
                        {
                            if (img != null && IsValidImage(img) && count < 2)
                            {
                                string fileName = $"proof_{requestId}_{count}_{Guid.NewGuid().ToString().Substring(0, 8)}{System.IO.Path.GetExtension(img.FileName)}";
                                string path = System.IO.Path.Combine(uploadDir, fileName);
                                img.SaveAs(path);

                                string imgSql = "INSERT INTO CompletionImages (RequestId, ImagePath, CreatedAt) VALUES (@ReqId, @Path, GETDATE())";
                                SqlCommand imgCmd = new SqlCommand(imgSql, conn, trans);
                                imgCmd.Parameters.AddWithValue("@ReqId", requestId);
                                imgCmd.Parameters.AddWithValue("@Path", "/Uploads/Completions/" + fileName);
                                imgCmd.ExecuteNonQuery();
                                count++;
                            }
                        }
                    }

                    trans.Commit();

                    // Notify Customer
                    using (SqlConnection notifyConn = new SqlConnection(GetConnectionString()))
                    {
                        notifyConn.Open();
                        string detSql = @"SELECT u.Email, u.FullName, r.Title, p.FullName AS ProvName, p.CompanyName 
                                          FROM ServiceRequests r 
                                          JOIN Users u ON r.CustomerID = u.Id 
                                          JOIN Users p ON r.SelectedProviderId = p.Id
                                          WHERE r.Id = @Id";
                        SqlCommand detCmd = new SqlCommand(detSql, notifyConn);
                        detCmd.Parameters.AddWithValue("@Id", requestId);
                        using (var r = detCmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                string custEmail = r["Email"].ToString();
                                string custName = r["FullName"].ToString();
                                string title = r["Title"].ToString();
                                string provName = r["CompanyName"].ToString() != "" ? r["CompanyName"].ToString() : r["ProvName"].ToString();
                                _emailService.NotifyCustomerOfJobCompletion(custEmail, custName, provName, title);
                            }
                        }
                    }

                    _auditService.LogActivity(userId.Value, "Job Marked Completed", $"Marked request {requestId} as completed. Pending customer confirmation.", Request.UserHostAddress);

                    return Json(new { success = true, message = "Job marked as completed. Waiting for customer confirmation." });
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateBusinessSettings(string bankName, string accountNumber, string branchCode, 
            string accountType, int serviceRadius, string availability, string about, bool isInstantPayment = false)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });
            if (serviceRadius < 1) serviceRadius = 1;

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = @"UPDATE Users 
                               SET BankName = @Bank, 
                                   AccountNumber = @Acc, 
                                   BranchCode = @Branch, 
                                   AccountType = @AccType,
                                   ServiceRadius = @Radius,
                                   Availability = @Avail,
                                   About = @About,
                                   IsInstantPayment = @Instant
                               WHERE Id = @Id";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Bank", bankName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Acc", accountNumber ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Branch", branchCode ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@AccType", accountType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Radius", serviceRadius);
                cmd.Parameters.AddWithValue("@Avail", availability ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@About", about ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Instant", isInstantPayment);
                cmd.Parameters.AddWithValue("@Id", userId);

                cmd.ExecuteNonQuery();

                _auditService.LogActivity(userId.Value, "Business Profile Updated", "Updated business and bank settings", Request.UserHostAddress);

                return Json(new { success = true, message = "Business settings updated successfully!" });
            }
        }

        [HttpPost]
        public ActionResult IncrementViewCount(int requestId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "UPDATE ServiceRequests SET ViewCount = ViewCount + 1 WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", requestId);
                cmd.ExecuteNonQuery();
                return Json(new { success = true });
            }
        }

        private void SyncExpiredRequests(SqlConnection conn)
        {
            string sql = @"UPDATE ServiceRequests 
                           SET    Status = 'Closed' 
                           WHERE  Status = 'Open' 
                           AND    CreatedAt < DATEADD(hour, -24, GETDATE()) 
                           AND    Id NOT IN (SELECT RequestId FROM Bids)";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
