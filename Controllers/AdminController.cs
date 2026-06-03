using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using System.Dynamic;

namespace CallUp.Controllers
{
    public class PendingRequestViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Budget { get; set; }
        public string Category { get; set; }
        public string Customer { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public List<string> ImagePaths { get; set; } = new List<string>();
        public List<string> Messages { get; set; } = new List<string>();
        public List<string> Documents { get; set; } = new List<string>();
        public string CompletionNotes { get; set; }
    }

    [CallUp.Attributes.AuthorizeRole("Admin")]
    [Authorize]
    public class AdminController : CallUpController
    {
        private CallUp.Services.IEmailService _emailService = new CallUp.Services.EmailService();
        private CallUp.Services.IAuditService _auditService = new CallUp.Services.AuditService();
        private CallUp.Services.ILogService _logService = new CallUp.Services.LogService();

        public ActionResult Index()
        {
            try
            {
                string role = Session["UserRole"]?.ToString();

            ViewBag.IsDashboard = true;
            ViewBag.UserRole = role;
            ViewBag.FullName = Session["FullName"]?.ToString();
            
            // Initialize lists to prevent NullReferenceException in view if error occurs later
            ViewBag.Users = new List<dynamic>();
            ViewBag.PendingUsers = new List<dynamic>();
            ViewBag.CompletedPayouts = new List<dynamic>();
            ViewBag.PendingPayouts = new List<dynamic>();
            ViewBag.PendingRequests = new List<PendingRequestViewModel>();
            ViewBag.AllRequests = new List<PendingRequestViewModel>();
            ViewBag.TotalPaidOut = 0m;
            ViewBag.TotalRevenue = 0m;

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SyncExpiredRequests(conn);
                
                ViewBag.TotalUsers     = GetCount(conn, "SELECT COUNT(*) FROM Users");
                ViewBag.TotalCustomers = GetCount(conn, "SELECT COUNT(*) FROM Users WHERE UserRole = 'Customer'");
                ViewBag.TotalProviders = GetCount(conn, "SELECT COUNT(*) FROM Users WHERE UserRole = 'Provider'");
                ViewBag.TotalRequests  = GetCount(conn, "SELECT COUNT(*) FROM ServiceRequests");
                ViewBag.OpenRequests   = GetCount(conn, "SELECT COUNT(*) FROM ServiceRequests WHERE Status = 'Open'");

                // All users for the management table
                var users = new List<dynamic>();
                string userSql = @"SELECT Id, FullName, Email, UserRole, IsActive, StatusReason,
                                          COALESCE(CompanyName,'') AS CompanyName,
                                          COALESCE(Location,'')    AS Location
                                   FROM   Users ORDER BY UserRole, FullName";
                using (var reader = new SqlCommand(userSql, conn).ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dynamic u = new ExpandoObject();
                        u.id          = (int)reader["Id"];
                        u.fullName    = reader["FullName"]?.ToString() ?? "Unknown";
                        u.email       = reader["Email"]?.ToString() ?? "";
                        u.role        = reader["UserRole"]?.ToString() ?? "User";
                        u.companyName = reader["CompanyName"]?.ToString() ?? "";
                        u.location    = reader["Location"]?.ToString() ?? "";
                        u.isActive    = reader["IsActive"] != DBNull.Value ? (bool)reader["IsActive"] : true;
                        u.statusReason = reader["StatusReason"]?.ToString() ?? "";
                        users.Add(u);
                    }
                }
                ViewBag.Users = users;


                // Pending User Approvals (IsApproved = 0)
                var pendingUsers = new List<dynamic>();
                string userPendSql = @"SELECT Id, FullName, Email, UserRole, CompanyName, Location, BankName, AccountNumber, BranchCode
                                       FROM   Users WHERE IsApproved = 0 AND UserRole != 'Admin'
                                       ORDER  BY CreatedAt DESC";
                using (var reader = new SqlCommand(userPendSql, conn).ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dynamic u = new ExpandoObject();
                        u.id = reader["Id"];
                        u.fullName = reader["FullName"].ToString();
                        u.email = reader["Email"].ToString();
                        u.role = reader["UserRole"].ToString();
                        u.company = reader["CompanyName"].ToString();
                        u.location = reader["Location"].ToString();
                        u.bank = reader["BankName"].ToString();
                        u.account = reader["AccountNumber"].ToString();
                        u.branch = reader["BranchCode"].ToString();
                        pendingUsers.Add(u);
                    }
                }
                ViewBag.PendingUsers = pendingUsers;

                // --- FINANCIAL SUMMARY ---
                decimal totalPaidOut = 0;
                decimal totalRevenue = 0;
                string finSql = "SELECT SUM(PayoutAmount) AS TotalPaid, SUM(ServiceFee) AS TotalRevenue FROM Payouts WHERE Status = 'Paid'";
                using (var reader = new SqlCommand(finSql, conn).ExecuteReader())
                {
                    if (reader.Read())
                    {
                        totalPaidOut = reader["TotalPaid"] != DBNull.Value ? (decimal)reader["TotalPaid"] : 0;
                        totalRevenue = reader["TotalRevenue"] != DBNull.Value ? (decimal)reader["TotalRevenue"] : 0;
                    }
                }
                ViewBag.TotalPaidOut = totalPaidOut;
                ViewBag.TotalRevenue = totalRevenue;

                // Step 3: Recent Payout History (Financial Report)
                var completedPayouts = new List<dynamic>();
                string histSql = @"SELECT TOP 20 p.RequestId, p.PayoutAmount, p.ServiceFee, p.TotalAmount, p.PaidAt, r.Title, u.FullName AS ProviderName
                                 FROM Payouts p
                                 JOIN ServiceRequests r ON p.RequestId = r.Id
                                 LEFT JOIN Users u ON p.ProviderId = u.Id
                                 WHERE p.Status = 'Paid'
                                 ORDER BY p.PaidAt DESC";
                using (var rdr = new SqlCommand(histSql, conn).ExecuteReader()) {
                    while (rdr.Read()) {
                        dynamic ph = new ExpandoObject();
                        ph.RequestId = (int)rdr["RequestId"];
                        ph.Amount = (decimal)rdr["PayoutAmount"];
                        ph.Fee = (decimal)rdr["ServiceFee"];
                        ph.Total = (decimal)rdr["TotalAmount"];
                        ph.Title = rdr["Title"].ToString();
                        ph.Provider = rdr["ProviderName"]?.ToString() ?? "Provider";
                        ph.PaidAt = (DateTime)rdr["PaidAt"];
                        completedPayouts.Add(ph);
                    }
                }
                ViewBag.CompletedPayouts = completedPayouts;

                // --- PENDING PAYOUTS (FINAL SYNC & FETCH) ---
                SyncLegacyPayouts(conn);
                var pendingPayouts = new List<dynamic>();
                string pendingSql = @"SELECT p.RequestId, p.PayoutAmount, p.ServiceFee, p.TotalAmount, p.Status, r.Title, r.Status AS RequestStatus, u.FullName AS ProviderName,
                                            u.BankName, u.AccountNumber, u.BranchCode
                                    FROM Payouts p
                                    JOIN ServiceRequests r ON p.RequestId = r.Id
                                    LEFT JOIN Users u ON p.ProviderId = u.Id
                                    WHERE p.Status != 'Paid'
                                    ORDER BY p.CreatedAt DESC";
                using (var reader = new SqlCommand(pendingSql, conn).ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dynamic p = new ExpandoObject();
                        p.RequestId = (int)reader["RequestId"];
                        p.Title = reader["Title"].ToString();
                        p.Provider = reader["ProviderName"]?.ToString() ?? "Unknown";
                        p.Amount = (decimal)reader["PayoutAmount"];
                        p.Fee = (decimal)reader["ServiceFee"];
                        p.Total = (decimal)reader["TotalAmount"];
                        p.Status = reader["Status"].ToString();
                        p.RequestStatus = reader["RequestStatus"].ToString();
                        p.BankName = reader["BankName"]?.ToString() ?? "N/A";
                        p.AccountNumber = reader["AccountNumber"]?.ToString() ?? "N/A";
                        p.BranchCode = reader["BranchCode"]?.ToString() ?? "N/A";
                        pendingPayouts.Add(p);
                    }
                }
                ViewBag.PendingPayouts = pendingPayouts;

                // Pending Request Moderation (Status = 'Moderation')
                var pendingRequests = new List<PendingRequestViewModel>();
                string reqPendSql = @"SELECT r.Id, r.Title, r.Description, r.Location, r.CreatedAt, r.PriceRange,
                                             c.Name AS CategoryName,
                                             u.FullName AS CustomerName, u.Email AS CustomerEmail, u.PhoneNumber
                                      FROM   ServiceRequests r
                                      JOIN   Users u ON r.CustomerID = u.Id
                                      LEFT JOIN Categories c ON r.CategoryID = c.Id
                                      WHERE  r.Status = 'Moderation'
                                      ORDER  BY r.CreatedAt DESC";
                using (var reader = new SqlCommand(reqPendSql, conn).ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var v_req = new PendingRequestViewModel {
                            Id = (int)reader["Id"],
                            Title = reader["Title"].ToString(),
                            Description = reader["Description"].ToString(),
                            Location = reader["Location"].ToString(),
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            Budget = reader["PriceRange"]?.ToString() ?? "Negotiable",
                            Category = reader["CategoryName"]?.ToString() ?? "General",
                            Customer = reader["CustomerName"].ToString(),
                            Email = reader["CustomerEmail"].ToString(),
                            Phone = reader["PhoneNumber"]?.ToString() ?? "No Phone"
                        };
                        
                        // Fetch images for this request
                        using (SqlConnection conn2 = new SqlConnection(GetConnectionString()))
                        {
                            conn2.Open();
                            string imgSql = "SELECT ImagePath FROM RequestImages WHERE RequestId = @Id";
                            SqlCommand imgCmd = new SqlCommand(imgSql, conn2);
                            imgCmd.Parameters.AddWithValue("@Id", v_req.Id);
                            using (var imgReader = imgCmd.ExecuteReader())
                            {
                                while (imgReader.Read())
                                {
                                    v_req.ImagePaths.Add(imgReader["ImagePath"].ToString());
                                }
                            }
                        }
                        pendingRequests.Add(v_req);
                    }
                }
                // 4. All Service Requests (Full Audit)
                var allRequests = new List<PendingRequestViewModel>();
                string allReqSql = @"SELECT r.Id, r.Title, r.Status, r.CreatedAt, r.PriceRange, r.CompletionNotes,
                                            c.Name AS CategoryName,
                                            u.FullName AS CustomerName, u.Email AS CustomerEmail, u.PhoneNumber
                                     FROM   ServiceRequests r
                                     JOIN   Users u ON r.CustomerID = u.Id
                                     LEFT JOIN Categories c ON r.CategoryID = c.Id
                                     ORDER  BY r.CreatedAt DESC";
                using (var reader = new SqlCommand(allReqSql, conn).ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var vm = new PendingRequestViewModel {
                            Id = (int)reader["Id"],
                            Title = reader["Title"].ToString(),
                            Status = reader["Status"].ToString(),
                            CreatedAt = (DateTime)reader["CreatedAt"],
                            Budget = reader["PriceRange"]?.ToString() ?? "Negotiable",
                            Category = reader["CategoryName"]?.ToString() ?? "General",
                            Customer = reader["CustomerName"].ToString(),
                            Email = reader["CustomerEmail"].ToString(),
                            Phone = reader["PhoneNumber"]?.ToString() ?? "N/A",
                            CompletionNotes = reader["CompletionNotes"]?.ToString()
                        };

                        // Fetch Bids (Messages) - Use separate connection to avoid MARS issues if not enabled
                        using (SqlConnection nestedConn = new SqlConnection(GetConnectionString()))
                        {
                            nestedConn.Open();
                            string msgSql = "SELECT Notes FROM Bids WHERE RequestId = @RId AND Notes IS NOT NULL";
                            using (SqlCommand mCmd = new SqlCommand(msgSql, nestedConn))
                            {
                                mCmd.Parameters.AddWithValue("@RId", vm.Id);
                                using (var mr = mCmd.ExecuteReader())
                                {
                                    while (mr.Read()) vm.Messages.Add(mr["Notes"].ToString());
                                }
                            }

                            // Fetch Documents (Completion Images)
                            string docSql = "SELECT ImagePath FROM CompletionImages WHERE RequestId = @RId";
                            using (SqlCommand dCmd = new SqlCommand(docSql, nestedConn))
                            {
                                dCmd.Parameters.AddWithValue("@RId", vm.Id);
                                using (var dr = dCmd.ExecuteReader())
                                {
                                    while (dr.Read()) vm.Documents.Add(dr["ImagePath"].ToString());
                                }
                            }
                        }

                        allRequests.Add(vm);
                    }
                }
                ViewBag.AllRequests = allRequests;
                ViewBag.PendingRequests = pendingRequests;
            }

            return View();
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "Index", Session["UserId"] as int?);
                TempData["Message"] = "An error occurred while loading admin dashboard.";
                TempData["MessageType"] = "error";
                return View();
            }
        }

        [AllowAnonymous]
        public ActionResult Setup(string key)
        {
            // Emergency access if DB is broken and admin can't log in
            if (!Request.IsAuthenticated && key != "CallUpAdminSync2026")
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.IsDashboard = Request.IsAuthenticated;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult RunDatabaseUpdate(string key)
        {
            try
            {
                // Emergency access check
                if (!Request.IsAuthenticated && key != "CallUpAdminSync2026")
                {
                    return Json(new { success = false, message = "Unauthorized access." });
                }
                string scriptPath = Server.MapPath("~/update_live_db.sql");
                if (!System.IO.File.Exists(scriptPath))
                {
                    return Json(new { success = false, message = "Migration script not found on server at " + scriptPath });
                }

                string script = System.IO.File.ReadAllText(scriptPath);
                int commandsRun = 0;

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    // Split script by GO (regex for robustness)
                    string[] commands = System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    foreach (string cmdText in commands)
                    {
                        if (string.IsNullOrWhiteSpace(cmdText)) continue;
                        using (SqlCommand cmd = new SqlCommand(cmdText, conn))
                        {
                            cmd.ExecuteNonQuery();
                            commandsRun++;
                        }
                    }
                }
                
                _auditService.LogActivity(Session["UserId"] as int? ?? 0, "Database Migration", $"Successfully executed {commandsRun} commands from update_live_db.sql", Request.UserHostAddress);

                return Json(new { success = true, message = $"Database updated successfully! {commandsRun} command blocks executed." });
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "RunDatabaseUpdate", Session["UserId"] as int?);
                return Json(new { success = false, message = "Update failed: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveUser(int userId)
        {
            try
            {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();

                string getSql = "SELECT FullName, Email, UserRole FROM Users WHERE Id = @Id";
                SqlCommand getCmd = new SqlCommand(getSql, conn);
                getCmd.Parameters.AddWithValue("@Id", userId);
                string fullName = "", email = "", role = "";
                using (var r = getCmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        fullName = r["FullName"].ToString();
                        email = r["Email"].ToString();
                        role = r["UserRole"].ToString();
                    }
                }

                string sql = "UPDATE Users SET IsApproved = 1, ApprovedAt = GETDATE() WHERE Id = @Id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", userId);
                cmd.ExecuteNonQuery();

                if (!string.IsNullOrEmpty(email))
                {
                    _emailService.NotifyUserOfAccountApproval(email, fullName, role);
                }

                int? adminId = Session["UserId"] as int?;
                if (adminId != null)
                    _auditService.LogActivity(adminId.Value, "User Approved", $"Verified user {fullName} ({email})", Request.UserHostAddress);

                return Json(new { success = true, message = "User account has been verified and activated." });
            }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "ApproveUser", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TestEmail(string targetEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(targetEmail)) targetEmail = "enkwinika@gmail.com";
                
                _emailService.SendVerificationCode(targetEmail, "Test User", "123456");
                
                int? adminId = Session["UserId"] as int?;
                if (adminId != null)
                    _auditService.LogActivity(adminId.Value, "Email Test", $"Sent manual test email to {targetEmail}", Request.UserHostAddress);

                return Json(new { success = true, message = "Test email sent successfully to " + targetEmail });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Email failed: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectUser(int userId, string reason)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    string sql = "UPDATE Users SET IsVerified = -1 WHERE Id = @Id"; // -1 for rejected
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.ExecuteNonQuery();

                    _auditService.LogActivity(Session["UserId"] as int? ?? 0, "User Rejected", $"Rejected user {userId}. Reason: {reason}", Request.UserHostAddress);
                    return Json(new { success = true, message = "User application rejected." });
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "RejectUser", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveRequest(int requestId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // 1. Get Request Details
                    string getSql = @"SELECT r.Title, r.Location, r.CategoryID, u.Email, u.FullName, r.Description 
                                      FROM ServiceRequests r 
                                      JOIN Users u ON r.CustomerID = u.Id 
                                      WHERE r.Id = @Id";
                    SqlCommand getCmd = new SqlCommand(getSql, conn, trans);
                    getCmd.Parameters.AddWithValue("@Id", requestId);
                    
                    string title = "", location = "", description = "", custEmail = "", custName = "";
                    int catId = 0;
                    using (var r = getCmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            title = r["Title"].ToString();
                            location = r["Location"].ToString();
                            description = r["Description"].ToString();
                            catId = (int)r["CategoryID"];
                            custEmail = r["Email"].ToString();
                            custName = r["FullName"].ToString();
                        }
                    }

                    // 2. Approve Request
                    string sql = "UPDATE ServiceRequests SET Status = 'Open' WHERE Id = @Id";
                    SqlCommand cmd = new SqlCommand(sql, conn, trans);
                    cmd.Parameters.AddWithValue("@Id", requestId);
                    cmd.ExecuteNonQuery();

                    // 3. Notify Matching Providers
                    string findProvSql = "SELECT Id, Email FROM Users WHERE UserRole = 'Provider' AND (CategoryId = @CatId OR CategoryId IS NULL) AND (Location LIKE '%' + @Loc + '%' OR @Loc = '') AND IsVerified = 1";
                    SqlCommand findCmd = new SqlCommand(findProvSql, conn, trans);
                    findCmd.Parameters.AddWithValue("@CatId", catId);
                    findCmd.Parameters.AddWithValue("@Loc", location ?? "");
                    
                    List<string> providerEmails = new List<string>();
                    List<int> providerIds = new List<int>();
                    using (var r = findCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            providerIds.Add((int)r["Id"]);
                            providerEmails.Add(r["Email"].ToString());
                        }
                    }

                    if (providerIds.Count > 0)
                    {
                        string msg = $"New Approved Lead: {title} in {location}";
                        foreach (var pId in providerIds)
                        {
                            string notifySql = "INSERT INTO Notifications (UserId, RequestId, Message, CreatedAt, IsRead) VALUES (@PId, @RId, @Msg, GETDATE(), 0)";
                            SqlCommand nCmd = new SqlCommand(notifySql, conn, trans);
                            nCmd.Parameters.AddWithValue("@PId", pId);
                            nCmd.Parameters.AddWithValue("@RId", requestId);
                            nCmd.Parameters.AddWithValue("@Msg", msg);
                            nCmd.ExecuteNonQuery();
                        }

                        // Send Emails to matching providers
                        foreach (var pEmail in providerEmails)
                        {
                            _emailService.NotifySupplierOfNewLead(pEmail, title, location, description);
                        }
                    }

                    // 4. Notify Customer
                    _emailService.NotifyCustomerOfRequestLive(custEmail, custName, title);

                    int? adminId = Session["UserId"] as int?;
                    if (adminId != null)
                        _auditService.LogActivity(adminId.Value, "Request Approved", $"Approved request {requestId}: {title}", Request.UserHostAddress);

                    trans.Commit();
                    return Json(new { success = true, message = "Request approved and relevant suppliers have been notified!" });
                }
                catch (Exception ex)
                {
                    _logService.LogException(ex, "Admin", "ApproveRequest", Session["UserId"] as int?);
                    trans.Rollback();
                    return Json(new { success = false, message = "Error: " + ex.Message });
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectRequest(int requestId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    string sql = "UPDATE ServiceRequests SET Status = 'Rejected' WHERE Id = @Id";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", requestId);
                    cmd.ExecuteNonQuery();

                    int? adminId = Session["UserId"] as int?;
                    if (adminId != null)
                        _auditService.LogActivity(adminId.Value, "Request Rejected", $"Rejected request {requestId}", Request.UserHostAddress);

                    return Json(new { success = true, message = "Request rejected." });
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "RejectRequest", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveAndPay(int requestId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // Get details for notification
                            string detSql = @"SELECT r.Title, u.FullName, u.Email, b.Amount
                                              FROM ServiceRequests r
                                              JOIN Users u ON r.SelectedProviderId = u.Id
                                              JOIN Bids b ON r.Id = b.RequestId AND r.SelectedProviderId = b.ProviderId
                                              WHERE r.Id = @Id";
                            SqlCommand detCmd = new SqlCommand(detSql, conn, trans);
                            detCmd.Parameters.AddWithValue("@Id", requestId);
                            
                            string title = "", fullName = "", email = "";
                            decimal amount = 0;
                            using (var r = detCmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    title = r["Title"].ToString();
                                    fullName = r["FullName"].ToString();
                                    email = r["Email"].ToString();
                                    amount = (decimal)r["Amount"];
                                }
                            }

                            // Admin approves the work and moves payout to 'Pending Payout'
                            string sql = "UPDATE ServiceRequests SET Status = 'Completed', CompletedAt = GETDATE() WHERE Id = @Id";
                            SqlCommand cmd = new SqlCommand(sql, conn, trans);
                            cmd.Parameters.AddWithValue("@Id", requestId);
                            cmd.ExecuteNonQuery();

                            // Update Payout Status to Pending Payout
                            string paySql = "UPDATE Payouts SET Status = 'Pending Payout' WHERE RequestId = @Id";
                            SqlCommand payCmd = new SqlCommand(paySql, conn, trans);
                            payCmd.Parameters.AddWithValue("@Id", requestId);
                            payCmd.ExecuteNonQuery();

                            trans.Commit();

                            if (!string.IsNullOrEmpty(email))
                            {
                                _emailService.NotifySupplierOfPaymentReleased(email, fullName, title, amount);
                            }

                            int? adminId = Session["UserId"] as int?;
                            if (adminId != null)
                                _auditService.LogActivity(adminId.Value, "Work Approved", $"Approved work and moved R{amount} payout to pending stage for request {requestId}: {title}", Request.UserHostAddress);

                            return Json(new { success = true, message = "Work approved! The payout is now pending final payment confirmation." });
                        }
                        catch (Exception)
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "ApproveAndPay", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmPayout(int requestId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    // Security: Verify that the customer has approved the work
                    string checkSql = "SELECT Status FROM ServiceRequests WHERE Id = @Id";
                    using (SqlCommand checkCmd = new SqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Id", requestId);
                        var reqStatus = checkCmd.ExecuteScalar()?.ToString();
                        if (reqStatus == "PendingConfirmation")
                        {
                            return Json(new { success = false, message = "Payout blocked: This job is still awaiting customer approval in their service history." });
                        }
                    }

                    // Update Payout Status to Paid
                    string sql = "UPDATE Payouts SET Status = 'Paid', PaidAt = GETDATE() WHERE RequestId = @Id AND Status != 'Paid'";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", requestId);
                    int updated = cmd.ExecuteNonQuery();

                    if (updated > 0)
                    {
                        // Also ensure the Request itself is marked as Completed
                        string reqSql = "UPDATE ServiceRequests SET Status = 'Completed' WHERE Id = @Id";
                        SqlCommand reqCmd = new SqlCommand(reqSql, conn);
                        reqCmd.Parameters.AddWithValue("@Id", requestId);
                        reqCmd.ExecuteNonQuery();

                        int? adminId = Session["UserId"] as int?;
                        if (adminId != null)
                            _auditService.LogActivity(adminId.Value, "Payout Confirmed", $"Confirmed payout for request {requestId}", Request.UserHostAddress);

                        return Json(new { success = true, message = "Payout confirmed and marked as Paid!" });
                    }
                    return Json(new { success = false, message = "Could not find a pending payout for this request." });
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "ConfirmPayout", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error processing payout: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult GetUserLogs(int userId)
        {
            try
            {
                var logs = _auditService.GetUserLogs(userId);
                var result = logs.Select(l => new {
                    action = l.Action,
                    details = l.Details,
                    ip = l.IPAddress,
                    date = l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                });
                return Json(result, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "GetUserLogs", Session["UserId"] as int?);
                return Json(new { error = "Unable to fetch logs" }, JsonRequestBehavior.AllowGet);
            }
        }

        private int GetCount(SqlConnection conn, string sql)
        {
            return (int)new SqlCommand(sql, conn).ExecuteScalar();
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

        public ActionResult SystemHealth()
        {
            try
            {
                var errorLogs = new List<dynamic>();
                var activityLogs = new List<dynamic>();

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    
                    // Fetch Error Logs
                    string errSql = "SELECT TOP 100 * FROM ErrorLogs ORDER BY CreatedAt DESC";
                    using (var r = new SqlCommand(errSql, conn).ExecuteReader())
                    {
                        while (r.Read())
                        {
                            errorLogs.Add(new {
                                id = r["Id"],
                                message = r["Message"]?.ToString() ?? "",
                                stackTrace = r["StackTrace"]?.ToString() ?? "",
                                controller = r["Controller"]?.ToString() ?? "",
                                action = r["Action"]?.ToString() ?? "",
                                userId = r["UserId"],
                                date = (DateTime)r["CreatedAt"]
                            });
                        }
                    }

                    // Fetch Activity Logs
                    string actSql = @"SELECT TOP 100 a.*, u.FullName 
                                      FROM ActivityLogs a 
                                      LEFT JOIN Users u ON a.UserId = u.Id 
                                      ORDER BY a.CreatedAt DESC";
                    using (var r = new SqlCommand(actSql, conn).ExecuteReader())
                    {
                        while (r.Read())
                        {
                            activityLogs.Add(new {
                                id = r["Id"],
                                userName = r["FullName"]?.ToString() ?? "Guest",
                                action = r["Action"]?.ToString() ?? "",
                                details = r["Details"]?.ToString() ?? "",
                                ip = r["IPAddress"]?.ToString() ?? "",
                                date = (DateTime)r["CreatedAt"]
                            });
                        }
                    }
                }

                ViewBag.ErrorLogs = errorLogs;
                ViewBag.ActivityLogs = activityLogs;
                return View();
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "SystemHealth", Session["UserId"] as int?);
                TempData["Message"] = "Unable to load logs.";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClearErrorLogs()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    new SqlCommand("DELETE FROM ErrorLogs", conn).ExecuteNonQuery();
                }
                return RedirectToAction("SystemHealth");
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "ClearErrorLogs", Session["UserId"] as int?);
                return RedirectToAction("SystemHealth");
            }
        }

        private void SyncLegacyPayouts(SqlConnection conn)
        {
            // Fix: Capture full financial data (Provider, Total, Fee, Net) for historical jobs
            string sql = @"INSERT INTO Payouts (RequestId, ProviderId, TotalAmount, ServiceFee, PayoutAmount, Status, CreatedAt)
                           SELECT r.Id, r.SelectedProviderId, b.Amount, b.Amount * 0.05, b.Amount * 0.95, 'Pending Approval', GETDATE()
                           FROM ServiceRequests r
                           JOIN Bids b ON r.Id = b.RequestId AND r.SelectedProviderId = b.ProviderId
                           WHERE (LOWER(r.Status) IN ('completed', 'closed', 'pendingconfirmation', 'pendingapproval'))
                           AND r.SelectedProviderId IS NOT NULL
                           AND r.Id NOT IN (SELECT RequestId FROM Payouts)";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUserStatus(int userId, bool isActive, string reason)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    string sql = "UPDATE Users SET IsActive = @Active, StatusReason = @Reason WHERE Id = @Id";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Active", isActive);
                    cmd.Parameters.AddWithValue("@Reason", reason ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Id", userId);
                    cmd.ExecuteNonQuery();

                    string action = isActive ? "User Activated" : "User Deactivated";
                    _auditService.LogActivity(Session["UserId"] as int? ?? 0, action, $"{action} for user {userId}. Reason: {reason}", Request.UserHostAddress);

                    return Json(new { success = true, message = $"User has been {(isActive ? "activated" : "deactivated")} successfully." });
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Admin", "ToggleUserStatus", Session["UserId"] as int?);
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}
