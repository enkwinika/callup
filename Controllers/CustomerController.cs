using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CallUp.Models;

namespace CallUp.Controllers
{
    [CallUp.Attributes.AuthorizeRole("Customer")]
    public class CustomerController : CallUpController
    {
        private CallUp.Services.IEmailService _emailService = new CallUp.Services.EmailService();
        private CallUp.Services.IAuditService _auditService = new CallUp.Services.AuditService();
        private CallUp.Services.IGeoService _geoService = new CallUp.Services.GeoService();
        private CallUp.Services.ILogService _logService = new CallUp.Services.LogService();

        public ActionResult Index()
        {
            try
            {
                // Guard: only Customers may access
                string role = Session["UserRole"]?.ToString();
            if (role != "Customer")
                return RedirectToAction("Login", "Account");

            ViewBag.IsDashboard = true;
            ViewBag.UserRole = role;
            ViewBag.FullName = Session["FullName"]?.ToString();

            var requests = new List<ServiceRequest>();

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SyncExpiredRequests(conn);
                
                string sql = @"SELECT r.Id, r.Title, r.Description, r.Location, r.Status,
                                      r.CreatedAt, r.ServiceDate, c.Name AS CategoryName, r.ViewCount, (SELECT COUNT(*) FROM Bids WHERE RequestID = r.Id) AS BidCount
                               FROM   ServiceRequests r
                               JOIN   Categories c ON r.CategoryID = c.Id
                               JOIN   Users u       ON r.CustomerID = u.Id
                               WHERE  u.Email = @Email
                               ORDER  BY r.CreatedAt DESC";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", User.Identity.Name);

                // Fetch User Details for the profile and modal
                string userSql = "SELECT FullName, Location, Email, PhoneNumber, ProfileImagePath FROM Users WHERE Email = @Email";
                SqlCommand userCmd = new SqlCommand(userSql, conn);
                userCmd.Parameters.AddWithValue("@Email", User.Identity.Name);
                using (var r = userCmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        ViewBag.UserLocation = r["Location"]?.ToString() ?? "";
                        ViewBag.FullName = r["FullName"]?.ToString() ?? "";
                        ViewBag.Email = r["Email"]?.ToString() ?? "";
                        ViewBag.PhoneNumber = r["PhoneNumber"]?.ToString() ?? "";
                        ViewBag.ProfileImagePath = r["ProfileImagePath"]?.ToString() ?? "";
                    }
                }

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        requests.Add(new ServiceRequest
                        {
                            Id          = (int)reader["Id"],
                            Title       = reader["Title"]?.ToString() ?? "Untitled",
                            Description = reader["Description"]?.ToString() ?? "",
                            Location    = reader["Location"]?.ToString() ?? "Unknown",
                            Status      = reader["Status"]?.ToString() ?? "Open",
                            CreatedAt   = (DateTime)reader["CreatedAt"],
                            ServiceDate = reader["ServiceDate"] as DateTime?,
                            CategoryName = reader["CategoryName"]?.ToString() ?? "General",
                            ViewCount   = reader["ViewCount"] != DBNull.Value ? (int)reader["ViewCount"] : 0,
                            BidCount    = reader["BidCount"] != DBNull.Value ? (int)reader["BidCount"] : 0
                        });
                    }
                    reader.Close();

                    // Fetch Completed History
                    var historyRequests = new List<dynamic>();
                    string historySql = @"SELECT r.Id, r.Title, r.CreatedAt, r.ServiceDate, r.CompletedAt,
                                                 u.FullName AS ProviderName, b.Amount,
                                                 (SELECT COUNT(*) FROM Ratings WHERE RequestId = r.Id) as Rated
                                          FROM   ServiceRequests r
                                          JOIN   Users u ON r.SelectedProviderId = u.Id
                                          JOIN   Bids b  ON r.SelectedProviderId = b.ProviderId AND r.Id = b.RequestId
                                          JOIN   Users cust ON r.CustomerID = cust.Id
                                          WHERE  cust.Email = @Email AND r.Status = 'Completed'
                                          ORDER  BY r.CompletedAt DESC";
                    SqlCommand historyCmd = new SqlCommand(historySql, conn);
                    historyCmd.Parameters.AddWithValue("@Email", User.Identity.Name);
                    using (var hr = historyCmd.ExecuteReader())
                    {
                        while (hr.Read())
                        {
                            historyRequests.Add(new {
                                Id = (int)hr["Id"],
                                Title = hr["Title"].ToString(),
                                ProviderName = hr["ProviderName"].ToString(),
                                Amount = (decimal)hr["Amount"],
                                CompletedAt = (DateTime)hr["CompletedAt"],
                                Rated = (int)hr["Rated"]
                            });
                        }
                    }
                    ViewBag.History = historyRequests;
                    ViewBag.TotalDone = historyRequests.Count;
                }

                // Fetch quotes for accepted requests
                foreach (var req in requests)
                {
                    if (req.Status == "Accepted")
                    {
                        string qSql = "SELECT Amount, CreatedAt FROM Quotes WHERE RequestId = @ReqId";
                        SqlCommand qCmd = new SqlCommand(qSql, conn);
                        qCmd.Parameters.AddWithValue("@ReqId", req.Id);
                        using (var qReader = qCmd.ExecuteReader())
                        {
                            if (qReader.Read())
                            {
                                req.SelectedQuote = new Quote {
                                    Amount = (decimal)qReader["Amount"],
                                    CreatedAt = (DateTime)qReader["CreatedAt"]
                                };
                            }
                        }
                    }
                }
            }

            return View(requests);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Customer", "Index", Session["UserId"] as int?);
                TempData["Message"] = "An error occurred while loading your dashboard.";
                TempData["MessageType"] = "error";
                return View(new List<ServiceRequest>());
            }
        }

        public ActionResult Search(string query, string category, double? userLat = null, double? userLng = null)
        {
            var results = new List<object>();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                string sql = @"
                    SELECT DISTINCT
                           u.FullName,
                           COALESCE(u.CompanyName, '')  AS CompanyName,
                           COALESCE(u.Location, '')     AS Location,
                           COALESCE(u.CompanyName, u.FullName) AS DisplayName,
                           UPPER(SUBSTRING(COALESCE(u.CompanyName, u.FullName, '?'), 1, 1)) AS Initials,
                           COALESCE(c.Name, 'General') AS Category,
                           u.Latitude, u.Longitude, u.Id as ProviderId,
                           COALESCE((SELECT AVG(CAST(Score AS DECIMAL(10,2))) FROM Ratings WHERE ToProviderId = u.Id), 0) AS AvgRating,
                           (SELECT COUNT(*) FROM Ratings WHERE ToProviderId = u.Id) AS ReviewCount";

                if (userLat.HasValue && userLng.HasValue) {
                    sql += @", (6371 * acos(cos(radians(@UserLat)) * cos(radians(u.Latitude)) * cos(radians(u.Longitude) - radians(@UserLng)) + sin(radians(@UserLat)) * sin(radians(u.Latitude)))) AS Distance ";
                } else {
                    sql += @", 0 AS Distance ";
                }

                sql += @" FROM   Users u
                    LEFT JOIN Categories c ON u.CategoryId = c.Id
                    WHERE  u.UserRole = 'Provider'
                      AND  u.IsVerified = 1
                      AND  (u.IsApproved = 1 OR u.IsApproved IS NULL)
                      AND  (@Query = ''
                            OR u.CompanyName LIKE '%' + @Query + '%'
                            OR u.FullName    LIKE '%' + @Query + '%'
                            OR u.Location    LIKE '%' + @Query + '%')
                      AND  (@Category = '' OR @Category = 'General' OR c.Name = @Category)
                    ORDER BY " + (userLat.HasValue ? "Distance ASC, " : "AvgRating DESC, ") + " u.FullName";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Query",    string.IsNullOrEmpty(query)    ? "" : query.Trim());
                cmd.Parameters.AddWithValue("@Category", string.IsNullOrEmpty(category) ? "" : category.Trim());
                if (userLat.HasValue) {
                    cmd.Parameters.AddWithValue("@UserLat", userLat.Value);
                    cmd.Parameters.AddWithValue("@UserLng", userLng.Value);
                }
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new {
                            id          = (int)reader["ProviderId"],
                            fullName    = reader["FullName"].ToString(),
                            companyName = reader["CompanyName"].ToString(),
                            displayName = reader["DisplayName"].ToString(),
                            location    = reader["Location"].ToString(),
                            category    = reader["Category"].ToString(),
                            initials    = reader["Initials"].ToString(),
                            lat         = reader["Latitude"] != DBNull.Value ? (double)reader["Latitude"] : 0,
                            lng         = reader["Longitude"] != DBNull.Value ? (double)reader["Longitude"] : 0,
                            avgRating   = Convert.ToDouble(reader["AvgRating"]),
                            reviewCount = (int)reader["ReviewCount"],
                            distanceInKm = reader["Distance"] != DBNull.Value ? Convert.ToDouble(reader["Distance"]) : 0
                        });
                    }
                }
            }
            return Json(results, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateRequest(string title, int categoryId, string description, string serviceDate, string location, string specialNotes, string priceRange, IEnumerable<HttpPostedFileBase> Images, double? Latitude = null, double? Longitude = null)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    string sql = @"INSERT INTO ServiceRequests (CustomerID, CategoryID, Title, Description, Location, Latitude, Longitude, ServiceDate, SpecialNotes, PriceRange, Status, CreatedAt)
                                   VALUES (@UserId, @CatId, @Title, @Desc, @Loc, @Lat, @Lon, @Date, @Notes, @Price, 'Moderation', GETDATE());
                                   SELECT SCOPE_IDENTITY();";
                    
                    SqlCommand cmd = new SqlCommand(sql, conn, trans);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@CatId", categoryId);
                    cmd.Parameters.AddWithValue("@Title", title ?? "");
                    cmd.Parameters.AddWithValue("@Desc", description ?? "");
                    cmd.Parameters.AddWithValue("@Loc", location ?? "");
                    cmd.Parameters.AddWithValue("@Lat", Latitude ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Lon", Longitude ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Date", string.IsNullOrEmpty(serviceDate) ? (object)DBNull.Value : DateTime.Parse(serviceDate));
                    string finalNotes = (specialNotes ?? "") + (string.IsNullOrEmpty(Request.Form["Urgency"]) ? "" : " [Urgency: " + Request.Form["Urgency"] + "]");
                    cmd.Parameters.AddWithValue("@Notes", finalNotes);
                    cmd.Parameters.AddWithValue("@Price", priceRange ?? "");

                    int requestId = Convert.ToInt32(cmd.ExecuteScalar());

                    // Handle Images
                    if (Images != null)
                    {
                        int count = 0;
                        string uploadDir = Server.MapPath("~/Uploads/Requests/");
                        if (!System.IO.Directory.Exists(uploadDir)) System.IO.Directory.CreateDirectory(uploadDir);

                        foreach (var img in Images)
                        {
                            if (img != null && IsValidImage(img) && count < 2)
                            {
                                string fileName = $"req_{requestId}_{count}_{Guid.NewGuid().ToString().Substring(0, 8)}{System.IO.Path.GetExtension(img.FileName)}";
                                string path = System.IO.Path.Combine(uploadDir, fileName);
                                img.SaveAs(path);

                                string imgSql = "INSERT INTO RequestImages (RequestId, ImagePath) VALUES (@ReqId, @Path)";
                                SqlCommand imgCmd = new SqlCommand(imgSql, conn, trans);
                                imgCmd.Parameters.AddWithValue("@ReqId", requestId);
                                imgCmd.Parameters.AddWithValue("@Path", "/Uploads/Requests/" + fileName);
                                imgCmd.ExecuteNonQuery();
                                count++;
                            }
                        }
                    }

                    trans.Commit();

                    // Notify Admin of new request
                    string custName = Session["FullName"]?.ToString() ?? "A customer";
                    _emailService.NotifyAdminOfNewRequest(title, custName, location, description);

                    _auditService.LogActivity((int)userId, "Request Created", $"New request: {title}", Request.UserHostAddress);

                    return Json(new { success = true, message = "Request submitted for administrator review. You will be notified once it is approved and open for bidding." });
                }
                catch (Exception ex)
                {
                    _logService.LogException(ex, "Customer", "CreateRequest", userId);
                    trans.Rollback();
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GetBids(int requestId)
        {
            var bids = new List<object>();
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    string sql = @"SELECT b.Id, b.Amount, b.Notes, b.ETA, b.CreatedAt, b.Status, u.FullName, u.CompanyName,
                                           (SELECT AVG(CAST(Score AS FLOAT)) FROM Ratings WHERE ToProviderId = u.Id) AS AvgRating,
                                           (SELECT COUNT(*) FROM Ratings WHERE ToProviderId = u.Id) AS ReviewCount
                                    FROM Bids b
                                    JOIN Users u ON b.ProviderId = u.Id
                                    WHERE b.RequestId = @ReqId
                                    ORDER BY b.Amount ASC";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@ReqId", requestId);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bids.Add(new {
                                id = reader["Id"],
                                amount = reader["Amount"],
                                notes = reader["Notes"].ToString(),
                                eta = reader["ETA"]?.ToString() ?? "",
                                status = reader["Status"].ToString(),
                                provider = reader["CompanyName"].ToString() != "" ? reader["CompanyName"].ToString() : reader["FullName"].ToString(),
                                date = Convert.ToDateTime(reader["CreatedAt"]).ToString("MMM dd"),
                                rating = reader["AvgRating"] != DBNull.Value ? Math.Round(Convert.ToDouble(reader["AvgRating"]), 1) : 0.0,
                                reviews = reader["ReviewCount"]
                            });
                        }
                    }
                }
                return Json(bids, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Customer", "GetBids", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error loading bids: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SelectProvider(int requestId, int bidId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // 1. Get bid info
                    string bidSql = @"SELECT b.ProviderId, b.Amount, u.Email, u.FullName, u.CompanyName
                                      FROM Bids b 
                                      JOIN Users u ON b.ProviderId = u.Id 
                                      WHERE b.Id = @BidId";
                    SqlCommand bidCmd = new SqlCommand(bidSql, conn, trans);
                    bidCmd.Parameters.AddWithValue("@BidId", bidId);
                    int providerId = 0;
                    decimal amount = 0;
                    string provEmail = "", provName = "";
                    using (var r = bidCmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            providerId = (int)r["ProviderId"];
                            amount = (decimal)r["Amount"];
                            provEmail = r["Email"].ToString();
                            provName = r["CompanyName"].ToString() != "" ? r["CompanyName"].ToString() : r["FullName"].ToString();
                        }
                    }

                    // Get Request title
                    string tSql = "SELECT Title FROM ServiceRequests WHERE Id = @ReqId";
                    SqlCommand tCmd = new SqlCommand(tSql, conn, trans);
                    tCmd.Parameters.AddWithValue("@ReqId", requestId);
                    string title = tCmd.ExecuteScalar()?.ToString() ?? "Your Job";

                    // 2. Update Request status and set winner
                    string updSql = "UPDATE ServiceRequests SET Status = 'PendingPayment', SelectedProviderId = @ProvId WHERE Id = @ReqId";
                    string updBidSql = "UPDATE Bids SET Status = 'Accepted' WHERE Id = @BidId";

                    SqlCommand updCmd = new SqlCommand(updSql, conn, trans);
                    updCmd.Parameters.AddWithValue("@ProvId", providerId);
                    updCmd.Parameters.AddWithValue("@ReqId", requestId);
                    updCmd.ExecuteNonQuery();

                    SqlCommand updBidCmd = new SqlCommand(updBidSql, conn, trans);
                    updBidCmd.Parameters.AddWithValue("@BidId", bidId);
                    updBidCmd.ExecuteNonQuery();

                    // 3. Generate Quote
                    string quoteSql = "INSERT INTO Quotes (RequestId, ProviderId, Amount) VALUES (@ReqId, @ProvId, @Amt)";
                    SqlCommand qCmd = new SqlCommand(quoteSql, conn, trans);
                    qCmd.Parameters.AddWithValue("@ReqId", requestId);
                    qCmd.Parameters.AddWithValue("@ProvId", providerId);
                    qCmd.Parameters.AddWithValue("@Amt", amount);
                    qCmd.ExecuteNonQuery();

                    trans.Commit();

                    // Notify Supplier
                    _emailService.NotifySupplierOfBidSelection(provEmail, provName, title, amount);

                    int? userId = Session["UserId"] as int?;
                    if (userId != null)
                    {
                        _auditService.LogActivity(userId.Value, "Bid Selected", $"Selected bid for request {requestId} (Amount: R{amount})", Request.UserHostAddress);
                    }

                    return Json(new { success = true, message = "Supplier selected. Please proceed to payment." });
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
        public ActionResult ProcessPayment(int requestId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                // Simulation: In a real app, this would integrate with a payment gateway (Shield/Escrow)
                string sql = "UPDATE ServiceRequests SET Status = 'InProgress', WorkOrderDate = GETDATE() WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", requestId);
                cmd.ExecuteNonQuery();

                int? userId = Session["UserId"] as int?;
                if (userId != null)
                {
                    _auditService.LogActivity(userId.Value, "Payment Processed", $"Payment processed for request {requestId}. Job starting.", Request.UserHostAddress);
                }

                return Json(new { success = true, message = "Payment successful! Work order has been issued to the supplier." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmWork(int requestId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                // Customer confirms they are happy with the work proof
                string sql = "UPDATE ServiceRequests SET Status = 'PendingApproval' WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", requestId);
                cmd.ExecuteNonQuery();

                return Json(new { success = true, message = "Work confirmed. Admin will now approve final payment release." });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(string fullName, string phoneNumber, HttpPostedFileBase profileImage)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string imgPath = null;

                if (profileImage != null && IsValidImage(profileImage))
                {
                    string uploadDir = Server.MapPath("~/Uploads/Profiles/");
                    if (!System.IO.Directory.Exists(uploadDir)) System.IO.Directory.CreateDirectory(uploadDir);

                    string fileName = $"profile_{userId}_{Guid.NewGuid().ToString().Substring(0, 8)}{System.IO.Path.GetExtension(profileImage.FileName)}";
                    imgPath = "/Uploads/Profiles/" + fileName;
                    profileImage.SaveAs(System.IO.Path.Combine(uploadDir, fileName));
                }

                string sql = "UPDATE Users SET FullName = @Name, PhoneNumber = @Phone" + 
                             (imgPath != null ? ", ProfileImagePath = @Img" : "") + 
                             " WHERE Id = @Id";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Name", fullName ?? "");
                cmd.Parameters.AddWithValue("@Phone", phoneNumber ?? "");
                cmd.Parameters.AddWithValue("@Id", userId);
                if (imgPath != null) cmd.Parameters.AddWithValue("@Img", imgPath);

                cmd.ExecuteNonQuery();

                // Update Session
                Session["FullName"] = fullName;

                return Json(new { success = true, message = "Profile updated successfully!" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitRating(int requestId, int score, string comment)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                
                // 1. Get Provider ID from the request
                string provSql = "SELECT SelectedProviderId FROM ServiceRequests WHERE Id = @Id AND CustomerID = @UserId AND Status = 'Completed'";
                SqlCommand provCmd = new SqlCommand(provSql, conn);
                provCmd.Parameters.AddWithValue("@Id", requestId);
                provCmd.Parameters.AddWithValue("@UserId", userId);
                var provId = provCmd.ExecuteScalar();

                if (provId == null || provId == DBNull.Value)
                    return Json(new { success = false, message = "Invalid request or job not completed." });

                // 2. Check if already rated
                string checkSql = "SELECT COUNT(*) FROM Ratings WHERE RequestId = @Id";
                SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@Id", requestId);
                if ((int)checkCmd.ExecuteScalar() > 0)
                    return Json(new { success = false, message = "You have already rated this provider for this job." });

                // 3. Insert Rating
                string sql = "INSERT INTO Ratings (RequestId, FromCustomerId, ToProviderId, Score, Comment, CreatedAt) VALUES (@RId, @CId, @PId, @Score, @Comment, GETDATE())";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@RId", requestId);
                cmd.Parameters.AddWithValue("@CId", userId);
                cmd.Parameters.AddWithValue("@PId", (int)provId);
                cmd.Parameters.AddWithValue("@Score", score);
                cmd.Parameters.AddWithValue("@Comment", comment ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();

                _auditService.LogActivity((int)userId, "Rating Submitted", $"Rated provider for request {requestId} with {score} stars", Request.UserHostAddress);

                return Json(new { success = true, message = "Thank you for your feedback!" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelRequest(int id)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                // Check if the request belongs to the user and is in a cancellable state (Moderation or Open)
                string sqlCheck = "SELECT Status FROM ServiceRequests WHERE Id = @Id AND CustomerID = @UserId";
                SqlCommand cmdCheck = new SqlCommand(sqlCheck, conn);
                cmdCheck.Parameters.AddWithValue("@Id", id);
                cmdCheck.Parameters.AddWithValue("@UserId", userId);
                var status = cmdCheck.ExecuteScalar()?.ToString();

                if (status == null) return Json(new { success = false, message = "Request not found." });
                if (status != "Moderation" && status != "Open") 
                    return Json(new { success = false, message = "Only pending or open requests can be cancelled." });

                string sqlUpdate = "UPDATE ServiceRequests SET Status = 'Cancelled' WHERE Id = @Id";
                SqlCommand cmdUpdate = new SqlCommand(sqlUpdate, conn);
                cmdUpdate.Parameters.AddWithValue("@Id", id);
                cmdUpdate.ExecuteNonQuery();

                _auditService.LogActivity(userId.Value, "Request Cancelled", $"Cancelled request {id}", Request.UserHostAddress);
                
                return Json(new { success = true, message = "Request has been cancelled." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DirectBook(int ProviderId, string Category, string Description, string Location, string Urgency)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // 1. Get CategoryId from name if possible
                    int categoryId = 1;
                    string catSql = "SELECT Id FROM Categories WHERE Name = @Name";
                    using (SqlCommand catCmd = new SqlCommand(catSql, conn, trans))
                    {
                        catCmd.Parameters.AddWithValue("@Name", Category ?? "General");
                        var result = catCmd.ExecuteScalar();
                        if (result != null) categoryId = (int)result;
                    }

                    // 2. Create ServiceRequest (set to Open so provider can bid/quote)
                    string title = (Category ?? "Service") + " - Direct Request";
                    string specialNotes = "[DIRECT BOOKING] " + (string.IsNullOrEmpty(Urgency) ? "" : "Urgency: " + Urgency);
                    
                    string sql = @"INSERT INTO ServiceRequests (CustomerID, CategoryID, Title, Description, Location, Status, CreatedAt, SpecialNotes, SelectedProviderId)
                                   VALUES (@UserId, @CatId, @Title, @Desc, @Loc, 'Open', GETDATE(), @Notes, @ProvId);
                                   SELECT SCOPE_IDENTITY();";

                    SqlCommand cmd = new SqlCommand(sql, conn, trans);
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@CatId", categoryId);
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Desc", Description ?? "");
                    cmd.Parameters.AddWithValue("@Loc", Location ?? "");
                    cmd.Parameters.AddWithValue("@Notes", specialNotes);
                    cmd.Parameters.AddWithValue("@ProvId", ProviderId);

                    int requestId = Convert.ToInt32(cmd.ExecuteScalar());

                    trans.Commit();

                    // Notify Provider
                    string provEmail = "", provName = "";
                    string provSql = "SELECT Email, FullName, CompanyName FROM Users WHERE Id = @Id";
                    using (SqlCommand pCmd = new SqlCommand(provSql, conn))
                    {
                        pCmd.Parameters.AddWithValue("@Id", ProviderId);
                        using (var r = pCmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                provEmail = r["Email"].ToString();
                                provName = r["CompanyName"].ToString() != "" ? r["CompanyName"].ToString() : r["FullName"].ToString();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(provEmail))
                    {
                        _emailService.NotifyAdminOfNewRequest(title, Session["FullName"]?.ToString() ?? "A customer", Location, Description);
                        // In a real app, we'd have a specific NotifyProviderOfDirectRequest
                    }

                    _auditService.LogActivity(userId.Value, "Direct Booking", $"Created direct request {requestId} for provider {ProviderId}", Request.UserHostAddress);

                    return Json(new { success = true, message = "Direct request sent! The professional has been notified to provide a quote." });
                }
                catch (Exception ex)
                {
                    if (trans.Connection != null) trans.Rollback();
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
        }

        [HttpGet]
        public ActionResult GetProviderProfile(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    
                    // 1. Fetch Basic Info & Stats
                    string userSql = @"
                        SELECT u.FullName, u.CompanyName, u.Location, COALESCE(c.Name, u.Category, 'General') as Category, u.About, u.ProfileImagePath, u.CreatedAt,
                               (SELECT COUNT(*) FROM ServiceRequests WHERE SelectedProviderId = @Id AND Status IN ('Completed', 'Paid')) as JobsDone,
                               (SELECT AVG(CAST(Score AS DECIMAL(10,2))) FROM Ratings WHERE ToProviderId = @Id) as AvgRating,
                               (SELECT COUNT(*) FROM Ratings WHERE ToProviderId = @Id) as ReviewCount
                        FROM Users u 
                        LEFT JOIN Categories c ON u.CategoryId = c.Id
                        WHERE u.Id = @Id";
                    
                    SqlCommand cmd = new SqlCommand(userSql, conn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    
                    dynamic profile = new System.Dynamic.ExpandoObject();
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            profile.Id = id;
                            profile.DisplayName = r["CompanyName"] != DBNull.Value && !string.IsNullOrEmpty(r["CompanyName"].ToString()) ? r["CompanyName"].ToString() : r["FullName"].ToString();
                            profile.FullName = r["FullName"].ToString();
                            profile.Location = r["Location"].ToString();
                            profile.Category = r["Category"].ToString();
                            profile.About = r["About"] != DBNull.Value ? r["About"].ToString() : "This professional hasn't added a bio yet.";
                            profile.ProfileImage = r["ProfileImagePath"] != DBNull.Value ? r["ProfileImagePath"].ToString().Replace("~/", "/") : "/Content/img/default-avatar.png";
                            profile.Joined = r.IsDBNull(r.GetOrdinal("CreatedAt")) ? "Recent" : ((DateTime)r["CreatedAt"]).ToString("MMM yyyy");
                            profile.JobsDone = (int)r["JobsDone"];
                            profile.AvgRating = r["AvgRating"] != DBNull.Value ? Convert.ToDouble(r["AvgRating"]) : 0;
                            profile.ReviewCount = (int)r["ReviewCount"];
                        }
                        else return Json(new { success = false, message = "Provider not found." }, JsonRequestBehavior.AllowGet);
                    }

                    // 2. Fetch Reviews
                    var reviews = new List<object>();
                    string reviewsSql = @"
                        SELECT r.Score, r.Comment, r.CreatedAt, u.FullName as CustomerName
                        FROM Ratings r
                        JOIN Users u ON r.FromCustomerId = u.Id
                        WHERE r.ToProviderId = @Id
                        ORDER BY r.CreatedAt DESC";
                    
                    SqlCommand cmdReviews = new SqlCommand(reviewsSql, conn);
                    cmdReviews.Parameters.AddWithValue("@Id", id);
                    
                    using (var r = cmdReviews.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            reviews.Add(new {
                                score = (int)r["Score"],
                                comment = r["Comment"].ToString(),
                                customer = r["CustomerName"].ToString().Split(' ')[0] + "...", // Privacy
                                date = ((DateTime)r["CreatedAt"]).ToString("dd MMM yyyy")
                            });
                        }
                    }

                    return Json(new { success = true, profile = profile, reviews = reviews }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message }, JsonRequestBehavior.AllowGet);
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
