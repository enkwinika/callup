using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.SignalR;
using CallUp.Models;
using CallUp.Hubs;
using CallUp.Services;

namespace CallUp.Controllers
{
    [CallUp.Attributes.AuthorizeRole("Customer", "Provider", "Admin")]
    public class DashboardController : CallUpController
    {
        private CallUpContext _db = new CallUpContext(); // Still needed for some Model definitions but we'll use SQL for data
        private IEmailService _emailService = new EmailService();
        private ILogService _logService = new LogService();
        private IAuditService _auditService = new AuditService();

        private string GetUserRole(string email)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                string sql = "SELECT UserRole FROM Users WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);
                conn.Open();
                return cmd.ExecuteScalar()?.ToString()?.Trim();
            }
        }

        private void LoadUserProfile(string email)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                string sql = @"SELECT FullName, PhoneNumber, Location, Latitude, Longitude, CreatedAt, IsApproved,
                                     BankName, AccountNumber, BranchCode, AccountType, ServiceRadius, Availability, IsInstantPayment, About 
                              FROM Users WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);
                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        ViewBag.FullName = r.SafeRead<string>("FullName");
                        ViewBag.PhoneNumber = r.SafeRead<string>("PhoneNumber");
                        ViewBag.UserLocation = r.SafeRead<string>("Location");
                        ViewBag.Latitude = r.SafeRead<double?>("Latitude");
                        ViewBag.Longitude = r.SafeRead<double?>("Longitude");
                        ViewBag.MemberSince = r.IsDBNull(r.GetOrdinal("CreatedAt")) ? DateTime.Now.Year : ((DateTime)r["CreatedAt"]).Year;
                        ViewBag.IsApproved = r.HasColumn("IsApproved") && (r["IsApproved"] != DBNull.Value && (bool)r["IsApproved"]);

                        // Bank & Business Details
                        ViewBag.BankName = r.SafeRead<string>("BankName");
                        ViewBag.AccountNumber = r.SafeRead<string>("AccountNumber");
                        ViewBag.BranchCode = r.SafeRead<string>("BranchCode");
                        ViewBag.AccountType = r.SafeRead<string>("AccountType");
                        ViewBag.ServiceRadius = r["ServiceRadius"] != DBNull.Value ? (int)r["ServiceRadius"] : 15;
                        ViewBag.Availability = r.SafeRead<string>("Availability");
                        ViewBag.IsInstantPayment = r["IsInstantPayment"] != DBNull.Value && (bool)r["IsInstantPayment"];
                        ViewBag.About = r.SafeRead<string>("About");
                    }
                }
            }
            ViewBag.GoogleMapsApiKey = System.Configuration.ConfigurationManager.AppSettings["GoogleMapsApiKey"];
        }

        public ActionResult Index()
        {
            try
            {
                string userEmail = User.Identity.Name;
                System.Diagnostics.Trace.WriteLine("logx: Dashboard/Index - Authenticated: " + userEmail);
                TempData["logx"] = "[Dashboard/Index] Entry for " + userEmail;
                LoadUserProfile(userEmail);

                string role = GetUserRole(userEmail);
                if (string.IsNullOrEmpty(role)) {
                    role = Session["UserRole"] as string;
                    System.Diagnostics.Trace.WriteLine("logx: Dashboard/Index - Role from DB empty, using Session: " + role);
                }
                
                System.Diagnostics.Trace.WriteLine("logx: Dashboard/Index - Role identified: " + role);
                ViewBag.IsDashboard = true;
                ViewBag.UserRole = role;

                if (string.IsNullOrEmpty(role)) {
                    System.Diagnostics.Trace.WriteLine("logx: Dashboard/Index - NO ROLE FOUND, redirecting to Home");
                    TempData["logx"] = "Dashboard/Index: No role found for user " + userEmail;
                    return RedirectToAction("Index", "Home");
                }

                if (role.Equals("Customer", StringComparison.OrdinalIgnoreCase)) {
                    TempData["logx"] = "[Dashboard/Index] Redirecting to Customer Action";
                    return RedirectToAction("Customer");
                }
                if (role.Equals("Provider", StringComparison.OrdinalIgnoreCase)) {
                    TempData["logx"] = "[Dashboard/Index] Redirecting to Provider Action";
                    return RedirectToAction("Provider");
                }
                if (role.Equals("Admin",    StringComparison.OrdinalIgnoreCase)) {
                    TempData["logx"] = "[Dashboard/Index] Redirecting to Admin Controller";
                    return RedirectToAction("Index", "Admin");
                }

                System.Diagnostics.Trace.WriteLine("logx: Dashboard/Index - Unknown role: " + role);
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("logx: Dashboard/Index ERROR: " + ex.ToString());
                TempData["logx"] = "Dashboard/Index Error: " + ex.Message;
                _logService.LogException(ex, "Dashboard", "Index", Session["UserId"] as int?);
                return RedirectToAction("Login", "Account");
            }
        }

        //public ActionResult Customerogg()
        //{
        //    try
        //    {
        //        if (!Request.IsAuthenticated) return RedirectToAction("Login", "Account");

        //        string userEmail = User.Identity.Name;
        //        LoadUserProfile(userEmail);

        //        string role = GetUserRole(userEmail);
        //        ViewBag.UserRole = role;
        //        ViewBag.IsDashboard = true;

        //        var requests = new List<ServiceRequest>();
        //        string connString = GetConnectionString();

        //        using (SqlConnection connection = new SqlConnection(connString))
        //        {
        //            string sql = @"SELECT r.Id, r.Description, r.Location, r.Status, r.CreatedAt, r.ExpiresAt, c.Name as CategoryName 
        //                         FROM ServiceRequests r 
        //                         JOIN Categories c ON r.CategoryID = c.Id
        //                         JOIN Users u ON r.CustomerID = u.Id
        //                         WHERE u.Email = @Email ORDER BY r.CreatedAt DESC";
        //            SqlCommand cmd = new SqlCommand(sql, connection);
        //            cmd.Parameters.AddWithValue("@Email", User.Identity.Name);
        //            connection.Open();
        //            using (SqlDataReader reader = cmd.ExecuteReader())
        //            {
        //                while (reader.Read())
        //                {
        //                    requests.Add(new ServiceRequest
        //                    {
        //                        Id = (int)reader["Id"],
        //                        Description = reader["Description"].ToString(),
        //                        Location = reader["Location"].ToString(),
        //                        Status = reader["Status"].ToString(),
        //                        CreatedAt = (DateTime)reader["CreatedAt"],
        //                        ExpiresAt = reader["ExpiresAt"] as DateTime?,
        //                        CategoryName = reader["CategoryName"].ToString()
        //                    });
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logService.LogException(ex, "Dashboard", "Customer", Session["UserId"] as int?);
        //        return RedirectToAction("Index", "Home");
        //    }
        //}

        private void LoadMessages(string email)
        {
            var messages = new List<dynamic>();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                // Fetch messages sent to this user
                string sql = @"SELECT m.Id, m.Content, m.CreatedAt, m.IsRead, u.FullName as SenderName, u.UserRole as Role, u.Id as SenderId
                             FROM Messages m
                             JOIN Users u ON m.SenderId = u.Id
                             JOIN Users target ON m.ReceiverId = target.Id
                             WHERE target.Email = @Email
                             ORDER BY m.CreatedAt DESC";
                
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", email);
                conn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        dynamic msg = new System.Dynamic.ExpandoObject();
                        msg.Id = r.SafeRead<int>("Id");
                        msg.SenderId = r.SafeRead<int>("SenderId");
                        msg.SenderName = r.SafeRead<string>("SenderName") ?? "User";
                        msg.Role = r.SafeRead<string>("Role") ?? "Guest";
                        msg.Message = r.SafeRead<string>("Content") ?? "";
                        msg.IsRead = r.SafeRead<bool>("IsRead");
                        msg.Time = r.IsDBNull(r.GetOrdinal("CreatedAt")) ? "" : ((DateTime)r["CreatedAt"]).ToString("ddd, hh:mm tt");
                        messages.Add(msg);
                    }
                }
            }
            ViewBag.Messages = messages;
        }

        public ActionResult Customer()
        {
            try
            {
                System.Diagnostics.Trace.WriteLine("logx: Entering Customer Action");
                if (!Request.IsAuthenticated) return RedirectToAction("Login", "Account");
                
                string userEmail = User.Identity.Name;
                System.Diagnostics.Trace.WriteLine("logx: Customer - User: " + userEmail);
                TempData["logx"] = "[Dashboard/Customer] Entry for " + userEmail;
                LoadUserProfile(userEmail);
                LoadMessages(userEmail);

                string role = GetUserRole(userEmail);
                ViewBag.UserRole = role;
                ViewBag.IsDashboard = true;
                
                var requests = new List<ServiceRequest>();
                string connString = GetConnectionString();
                System.Diagnostics.Trace.WriteLine("logx: Customer - Opening DB Connection");

                using (SqlConnection connection = new SqlConnection(connString))
                {
                    string sql = @"SELECT r.Id, r.Description, r.Location, r.Status, r.CreatedAt, r.ExpiresAt, r.ViewCount,
                                          (SELECT COUNT(*) FROM Bids WHERE RequestId = r.Id) as BidCount,
                                          c.Name as CategoryName 
                                   FROM ServiceRequests r 
                                   JOIN Categories c ON r.CategoryID = c.Id
                                   JOIN Users u ON r.CustomerID = u.Id
                                   WHERE u.Email = @Email ORDER BY r.CreatedAt DESC";
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Email", User.Identity.Name);
                    connection.Open();
                    System.Diagnostics.Trace.WriteLine("logx: Customer - DB Opened, executing reader");
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add(new ServiceRequest
                            {
                                Id = reader.SafeRead<int>("Id"),
                                Description = reader.SafeRead<string>("Description"),
                                Location = reader.SafeRead<string>("Location"),
                                Status = reader.SafeRead<string>("Status"),
                                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : (DateTime)reader["CreatedAt"],
                                ExpiresAt = reader.SafeRead<DateTime?>("ExpiresAt"),
                                ViewCount = reader.SafeRead<int>("ViewCount"),
                                BidCount = reader.SafeRead<int>("BidCount"),
                                CategoryName = reader.SafeRead<string>("CategoryName")
                            });
                        }
                    }
                    System.Diagnostics.Trace.WriteLine("logx: Customer - Reader finished, count: " + requests.Count);
                }

                // Fetch History (Completed or Closed)
                var history = new List<dynamic>();
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    string histSql = @"SELECT r.Id, r.Title, r.Description, r.Status, r.CreatedAt, 
                                             p.Amount as BidAmount, u.FullName as ProviderName,
                                             pay.TotalAmount as PaidAmount
                                      FROM ServiceRequests r
                                      LEFT JOIN Bids p ON r.Id = p.RequestId AND r.SelectedProviderId = p.ProviderId
                                      LEFT JOIN Users u ON r.SelectedProviderId = u.Id
                                      LEFT JOIN Payouts pay ON r.Id = pay.RequestId
                                      WHERE r.CustomerID = (SELECT Id FROM Users WHERE Email = @Email)
                                      AND r.Status IN ('Completed', 'Closed', 'Cancelled', 'Rejected', 'PendingConfirmation')
                                      ORDER BY r.CreatedAt DESC";
                    
                    SqlCommand hCmd = new SqlCommand(histSql, connection);
                    hCmd.Parameters.AddWithValue("@Email", User.Identity.Name);
                    connection.Open();
                    using (SqlDataReader hr = hCmd.ExecuteReader())
                    {
                        while (hr.Read())
                        {
                            dynamic h = new System.Dynamic.ExpandoObject();
                            h.Id = (int)hr["Id"];
                            h.Title = hr["Title"]?.ToString() ?? hr["Description"]?.ToString() ?? "Service Request";
                            h.Status = hr["Status"].ToString();
                            h.CompletedAt = (DateTime)hr["CreatedAt"]; // Best approximation if Dedicated CompletedAt is missing
                            h.ProviderName = hr["ProviderName"]?.ToString() ?? "N/A";
                            h.Amount = (decimal)(hr["PaidAmount"] != DBNull.Value ? hr["PaidAmount"] : (hr["BidAmount"] != DBNull.Value ? hr["BidAmount"] : 0m));
                            h.Rated = 0; // Placeholder until Rating check is added
                            
                            // Fetch Completion Images
                            h.CompletionImages = new List<string>();
                            using (SqlConnection nestedConn = new SqlConnection(connString))
                            {
                                nestedConn.Open();
                                string imgSql = "SELECT ImagePath FROM CompletionImages WHERE RequestId = @RId";
                                using (SqlCommand imgCmd = new SqlCommand(imgSql, nestedConn))
                                {
                                    imgCmd.Parameters.AddWithValue("@RId", h.Id);
                                    using (var ir = imgCmd.ExecuteReader())
                                    {
                                        while (ir.Read()) h.CompletionImages.Add(ir["ImagePath"].ToString());
                                    }
                                }
                            }

                            history.Add(h);
                        }
                    }
                }
                ViewBag.History = history;

                return View(requests);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("logx: Customer ERROR: " + ex.ToString());
                TempData["logx"] = "Dashboard/Customer Error: " + ex.Message + " | Stack: " + ex.StackTrace;
                _logService.LogException(ex, "Dashboard", "Customer", Session["UserId"] as int?);
                return RedirectToAction("Index", "Home");
            }
        }

        public ActionResult Provider()
        {
            try
            {
                System.Diagnostics.Trace.WriteLine("logx: Entering Provider Action");
                if (!Request.IsAuthenticated) return RedirectToAction("Login", "Account");
                
                string userEmail = User.Identity.Name;
                System.Diagnostics.Trace.WriteLine("logx: Provider - User: " + userEmail);
                TempData["logx"] = "[Dashboard/Provider] Entry for " + userEmail;
                LoadUserProfile(userEmail);
                LoadMessages(userEmail);

                string role = GetUserRole(userEmail);
                ViewBag.UserRole = role;
                ViewBag.IsDashboard = true;

                double? provLat = ViewBag.Latitude as double?;
                double? provLng = ViewBag.Longitude as double?;

                var leads = new List<ServiceRequest>();
                var myJobs = new List<ServiceRequest>();

                System.Diagnostics.Trace.WriteLine("logx: Provider - Opening DB for leads/jobs");
                int providerId = 0;
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();

                    // 1. Get Provider Detailed Stats
                    string statsSql = @"
                        SELECT u.Id,
                               COALESCE((SELECT SUM(Amount) FROM Bids WHERE ProviderId = u.Id AND Status IN ('Accepted', 'Selected', 'InProgress', 'Completed')), 0) as TotalEarned,
                               (SELECT COUNT(*) FROM Bids WHERE ProviderId = u.Id AND Status IN ('Accepted', 'Selected', 'InProgress', 'Completed')) as JobsWon,
                               (SELECT COUNT(*) FROM Bids WHERE ProviderId = u.Id) as BidsPlaced,
                               COALESCE((SELECT AVG(CAST(Score AS DECIMAL(10,2))) FROM Ratings WHERE ToProviderId = u.Id), 0) as AvgRating,
                               (SELECT COUNT(*) FROM Ratings WHERE ToProviderId = u.Id) as ReviewCount
                        FROM Users u WHERE u.Email = @Email";
                    
                    using (SqlCommand sCmd = new SqlCommand(statsSql, conn))
                    {
                        sCmd.Parameters.AddWithValue("@Email", userEmail);
                        using (var sr = sCmd.ExecuteReader())
                        {
                            if (sr.Read())
                            {
                                providerId = (int)sr["Id"];
                                ViewBag.TotalEarned = Convert.ToDecimal(sr["TotalEarned"]);
                                ViewBag.JobsWon = (int)sr["JobsWon"];
                                ViewBag.BidsPlaced = (int)sr["BidsPlaced"];
                                ViewBag.AvgRating = Convert.ToDouble(sr["AvgRating"]);
                                ViewBag.ReviewCount = (int)sr["ReviewCount"];
                            }
                        }
                    }

                    // 2. Calculate Active Leads (In Progress jobs)
                    string activeSql = "SELECT COUNT(*) FROM ServiceRequests WHERE SelectedProviderId = @PId AND Status IN ('Payment', 'Started', 'Review')";
                    using (SqlCommand aCmd = new SqlCommand(activeSql, conn))
                    {
                        aCmd.Parameters.AddWithValue("@PId", providerId);
                        ViewBag.ActiveJobs = (int)aCmd.ExecuteScalar();
                    }

                    // 1. Fetch Open Leads (Marketplace)
                    string leadsSql = @"SELECT r.*, c.Name as CategoryName,
                                       (SELECT COUNT(*) FROM Bids WHERE RequestId = r.Id) as BidCount,
                                       (SELECT MIN(Amount) FROM Bids WHERE RequestId = r.Id) as LowestBid
                                     FROM ServiceRequests r
                                     JOIN Categories c ON r.CategoryID = c.Id
                                     WHERE r.Status = 'Open'
                                       AND r.CustomerID != (SELECT Id FROM Users WHERE Email = @Email)
                                     ORDER BY r.CreatedAt DESC";
                    
                    SqlCommand leadsCmd = new SqlCommand(leadsSql, conn);
                    leadsCmd.Parameters.AddWithValue("@Email", userEmail);
                    using (var reader = leadsCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var req = new ServiceRequest
                            {
                                Id = reader.SafeRead<int>("Id"),
                                Title = reader.SafeRead<string>("Title"),
                                Description = reader.SafeRead<string>("Description"),
                                Location = reader.SafeRead<string>("Location"),
                                CategoryName = reader.SafeRead<string>("CategoryName"),
                                PriceRange = reader.SafeRead<string>("PriceRange"),
                                ViewCount = reader.SafeRead<int>("ViewCount"),
                                Latitude = reader.SafeRead<double?>("Latitude"),
                                Longitude = reader.SafeRead<double?>("Longitude"),
                                CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? DateTime.Now : (DateTime)reader["CreatedAt"],
                                BidCount = reader.SafeRead<int>("BidCount"),
                                LowestBid = reader.SafeRead<decimal?>("LowestBid")
                            };

                            if (provLat.HasValue && provLng.HasValue && req.Latitude.HasValue && req.Longitude.HasValue)
                            {
                                req.DistanceInKm = GetDistance(provLat.Value, provLng.Value, req.Latitude.Value, req.Longitude.Value);
                            }

                            // Fetch Images for this Request
                            using (SqlConnection imgConn = new SqlConnection(GetConnectionString()))
                            {
                                imgConn.Open();
                                string imgSql = "SELECT ImagePath FROM RequestImages WHERE RequestId = @ReqId";
                                using (SqlCommand imgCmd = new SqlCommand(imgSql, imgConn))
                                {
                                    imgCmd.Parameters.AddWithValue("@ReqId", req.Id);
                                    using (var imgReader = imgCmd.ExecuteReader())
                                    {
                                        while (imgReader.Read())
                                        {
                                            req.ImagePaths.Add(imgReader["ImagePath"].ToString());
                                        }
                                    }
                                }
                            }

                            leads.Add(req);
                        }
                    }
                    System.Diagnostics.Trace.WriteLine("logx: Provider - Leads loaded: " + leads.Count);

                    // 2. Fetch My Jobs (Accepted or Pending Payment)
                    string jobsSql = @"SELECT r.Id, r.Title, r.Location, r.Status, r.Latitude, r.Longitude, r.Description,
                                       c.FullName as CustomerName, c.PhoneNumber as CustomerPhone, c.Email as CustomerEmail,
                                       b.Amount, b.ETA
                                     FROM ServiceRequests r
                                     JOIN Users c ON r.CustomerID = c.Id
                                     JOIN Bids b ON r.Id = b.RequestId AND b.ProviderId = r.SelectedProviderId
                                     WHERE r.SelectedProviderId = (SELECT Id FROM Users WHERE Email = @Email)
                                       AND r.Status IN ('Accepted', 'PendingPayment', 'InProgress', 'PendingApproval')";
                    
                    SqlCommand jobsCmd = new SqlCommand(jobsSql, conn);
                    jobsCmd.Parameters.AddWithValue("@Email", userEmail);
                    using (var reader = jobsCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                                var job = new ServiceRequest {
                                    Id = reader.SafeRead<int>("Id"),
                                    Title = reader.SafeRead<string>("Title"),
                                    Location = reader.SafeRead<string>("Location"),
                                    Status = reader.SafeRead<string>("Status"),
                                    Description = reader.SafeRead<string>("Description"),
                                    CustomerName = reader.SafeRead<string>("CustomerName"),
                                    CustomerPhone = reader.SafeRead<string>("CustomerPhone"),
                                    CustomerEmail = reader.SafeRead<string>("CustomerEmail"),
                                    Amount = reader.SafeRead<decimal>("Amount"),
                                    ETA = reader.SafeRead<string>("ETA")
                                };

                                // Fetch Images for this Active Job
                                using (SqlConnection imgConn = new SqlConnection(GetConnectionString()))
                                {
                                    imgConn.Open();
                                    string imgSql = "SELECT ImagePath FROM RequestImages WHERE RequestId = @ReqId";
                                    using (SqlCommand imgCmd = new SqlCommand(imgSql, imgConn))
                                    {
                                        imgCmd.Parameters.AddWithValue("@ReqId", job.Id);
                                        using (var imgReader = imgCmd.ExecuteReader())
                                        {
                                            while (imgReader.Read())
                                            {
                                                job.ImagePaths.Add(imgReader["ImagePath"].ToString());
                                            }
                                        }
                                    }
                                }

                                myJobs.Add(job);
                        }
                    }
                    System.Diagnostics.Trace.WriteLine("logx: Provider - Jobs loaded: " + myJobs.Count);
                }

                ViewBag.MyJobs = myJobs;

                // 3. Fetch Earnings & Payouts History
                var earnings = new List<dynamic>();
                decimal totalEarned = 0;
                string earningsSql = @"SELECT p.PayoutAmount, p.TotalAmount, p.ServiceFee, p.Status, p.CreatedAt, p.PaidAt, r.Title 
                                       FROM Payouts p
                                       JOIN ServiceRequests r ON p.RequestId = r.Id
                                       WHERE p.ProviderId = (SELECT Id FROM Users WHERE Email = @Email)
                                       ORDER BY p.CreatedAt DESC";
                
                using (SqlConnection connEarnings = new SqlConnection(GetConnectionString()))
                {
                    connEarnings.Open();
                    SqlCommand earnCmd = new SqlCommand(earningsSql, connEarnings);
                    earnCmd.Parameters.AddWithValue("@Email", userEmail);
                    using (var reader = earnCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dynamic e = new System.Dynamic.ExpandoObject();
                            e.Amount = (decimal)reader["PayoutAmount"];
                            e.Total = (decimal)reader["TotalAmount"];
                            e.Fee = (decimal)reader["ServiceFee"];
                            e.Status = reader["Status"].ToString();
                            e.Project = reader["Title"].ToString();
                            e.Date = reader["CreatedAt"] != DBNull.Value ? ((DateTime)reader["CreatedAt"]).ToString("MMM dd, yyyy") : "";
                            earnings.Add(e);

                            if (e.Status == "Paid")
                            {
                                totalEarned += e.Amount;
                            }
                        }
                    }
                }
                ViewBag.Earnings = earnings;
                ViewBag.TotalEarned = totalEarned;

                return View(leads);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("logx: Provider ERROR: " + ex.ToString());
                TempData["logx"] = "Dashboard/Provider Error: " + ex.Message + " | Stack: " + ex.StackTrace;
                _logService.LogException(ex, "Dashboard", "Provider", Session["UserId"] as int?);
                return RedirectToAction("Index", "Home");
            }
        }

        private double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
                ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; // Distance in km
        }

        private double ToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }

        [HttpPost]
        public ActionResult CreateRequest(string Description, string Location, int CategoryId, double? lat, double? lng)
        {
            string email = User.Identity.Name;
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                string sql = @"INSERT INTO ServiceRequests (CustomerID, CategoryID, Description, Location, Status, CreatedAt, ExpiresAt, Latitude, Longitude)
                             SELECT Id, @CatId, @Desc, @Loc, 'Open', GETDATE(), DATEADD(MINUTE, 10, GETDATE()), @Lat, @Lng
                             FROM Users WHERE Email = @Email";
                
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@CatId", CategoryId);
                cmd.Parameters.AddWithValue("@Desc", Description);
                cmd.Parameters.AddWithValue("@Loc", Location);
                cmd.Parameters.AddWithValue("@Lat", lat ?? -26.1076);
                cmd.Parameters.AddWithValue("@Lng", lng ?? 28.0567);
                cmd.Parameters.AddWithValue("@Email", email);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Request successfully created! Watch for bids.";
            TempData["MessageType"] = "success";
            return RedirectToAction("Customer");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CallUp.Attributes.AuthorizeRole("Customer")]
        public ActionResult AcceptBid(int bidId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    SqlTransaction trans = conn.BeginTransaction();
                    try
                    {
                        // 0. Verify Request is still OPEN and get Provider info
                        string checkSql = @"SELECT r.Status, b.ProviderId, b.RequestId FROM ServiceRequests r 
                                          JOIN Bids b ON r.Id = b.RequestId 
                                          WHERE b.Id = @BidId";
                        SqlCommand checkCmd = new SqlCommand(checkSql, conn, trans);
                        checkCmd.Parameters.AddWithValue("@BidId", bidId);
                        
                        string status = "";
                        int providerId = 0;
                        int requestId = 0;

                        using (var rinfo = checkCmd.ExecuteReader()) {
                            if (rinfo.Read()) {
                                status = rinfo["Status"].ToString();
                                providerId = (int)rinfo["ProviderId"];
                                requestId = (int)rinfo["RequestId"];
                            }
                        }

                        if (status != "Open")
                        {
                            TempData["Message"] = "This request is no longer open for bid acceptance.";
                            TempData["MessageType"] = "error";
                            return RedirectToAction("Customer");
                        }

                        // 1. Update Bid Status
                        string sqlBid = "UPDATE Bids SET Status = 'Accepted' WHERE Id = @BidId";
                        SqlCommand cmdBid = new SqlCommand(sqlBid, conn, trans);
                        cmdBid.Parameters.AddWithValue("@BidId", bidId);
                        cmdBid.ExecuteNonQuery();

                        // 2. Update Request Status and Selection
                        string sqlReq = "UPDATE ServiceRequests SET Status = 'PendingPayment', SelectedProviderId = @ProvId WHERE Id = @ReqId";
                        SqlCommand cmdReq = new SqlCommand(sqlReq, conn, trans);
                        cmdReq.Parameters.AddWithValue("@ProvId", providerId);
                        cmdReq.Parameters.AddWithValue("@ReqId", requestId);
                        cmdReq.ExecuteNonQuery();

                        trans.Commit();
                        TempData["Message"] = "Bid accepted! Please proceed to payment to secure the professional.";
                        TempData["MessageType"] = "success";
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        _logService.LogException(ex, "Dashboard", "AcceptBid", Session["UserId"] as int?);
                        TempData["Message"] = "Error accepting bid: " + ex.Message;
                        TempData["MessageType"] = "error";
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Dashboard", "AcceptBid", Session["UserId"] as int?);
            }

            return RedirectToAction("Customer");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessSecurePayment(int requestId)
        {
            // Redirect to Adumo Initialize to handle the gateway handshake
            return RedirectToAction("Initialize", "Adumo", new { requestId = requestId });
        }


        [HttpGet]
        public ActionResult GetNotifications()
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false }, JsonRequestBehavior.AllowGet);

            var list = new List<object>();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                // 1. Fetch Standard Notifications
                string sql = "SELECT TOP 10 * FROM Notifications WHERE UserId = @UserId ORDER BY CreatedAt DESC";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new {
                            id = r["Id"],
                            type = "notification",
                            message = r["Message"].ToString(),
                            isRead = (bool)r["IsRead"],
                            date = ((DateTime)r["CreatedAt"]).ToString("HH:mm")
                        });
                    }
                }

                // 2. Fetch Unread Messages as notifications
                string msgSql = @"SELECT TOP 5 m.Id, m.Content, m.CreatedAt, u.FullName as SenderName 
                                FROM Messages m JOIN Users u ON m.SenderId = u.Id 
                                WHERE m.ReceiverId = @UserId AND m.IsRead = 0 
                                ORDER BY m.CreatedAt DESC";
                using (SqlCommand mCmd = new SqlCommand(msgSql, conn))
                {
                    mCmd.Parameters.AddWithValue("@UserId", userId);
                    using (var r = mCmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new {
                                id = r["Id"],
                                type = "message",
                                message = "New message from " + r["SenderName"] + ": " + (r["Content"].ToString().Length > 50 ? r["Content"].ToString().Substring(0, 47) + "..." : r["Content"].ToString()),
                                isRead = false,
                                date = ((DateTime)r["CreatedAt"]).ToString("HH:mm")
                            });
                        }
                    }
                }
            }

            // Sort combined list by date desc (closest to now first)
            // Note: Since we only have time strings in the objects above for UI, we might need to sort before formatting if we wanted perfect sorting.
            // For now, these are already top 10 / top 5 latest.

            return Json(new { success = true, notifications = list }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkMessagesAsRead()
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "UPDATE Messages SET IsRead = 1 WHERE ReceiverId = @UserId AND IsRead = 0";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.ExecuteNonQuery();
            }
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarkNotificationRead(int id)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false });

            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                string sql = "UPDATE Notifications SET IsRead = 1 WHERE Id = @Id AND UserId = @UserId";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.ExecuteNonQuery();
            }
            return Json(new { success = true });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(string fullName, string phoneNumber, string location)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    string sql = "UPDATE Users SET FullName = @Name, PhoneNumber = @Phone, Location = @Loc WHERE Id = @Id";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Name", fullName ?? "");
                    cmd.Parameters.AddWithValue("@Phone", phoneNumber ?? "");
                    cmd.Parameters.AddWithValue("@Loc", location ?? "");
                    cmd.Parameters.AddWithValue("@Id", userId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                // Update Session
                Session["FullName"] = fullName;

                return Json(new { success = true, message = "Profile updated successfully!" });
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Dashboard", "UpdateProfile", userId);
                return Json(new { success = false, message = "Error updating profile: " + ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmJob(int requestId)
        {
            int? userId = Session["UserId"] as int?;
            if (userId == null) return Json(new { success = false, message = "Session expired." });

            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    // 1. Verify owner and current status
                    string checkSql = "SELECT Status FROM ServiceRequests WHERE Id = @Id AND CustomerID = @CId";
                    SqlCommand checkCmd = new SqlCommand(checkSql, conn);
                    checkCmd.Parameters.AddWithValue("@Id", requestId);
                    checkCmd.Parameters.AddWithValue("@CId", userId);
                    
                    var currentStatus = checkCmd.ExecuteScalar()?.ToString();
                    if (currentStatus != "PendingConfirmation") {
                        return Json(new { success = false, message = "This job is not awaiting confirmation. Current status: " + currentStatus });
                    }

                    // 2. Update to Completed
                    string sql = "UPDATE ServiceRequests SET Status = 'Completed' WHERE Id = @Id";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", requestId);
                    cmd.ExecuteNonQuery();

                    // 3. Log the audit
                    _auditService.LogActivity(userId.Value, "Job Confirmed", "Request #" + requestId + " has been confirmed by customer.", Request.UserHostAddress);
                }

                return Json(new { success = true, message = "Job confirmed! You can now rate the provider." });
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, "Dashboard", "ConfirmJob", userId);
                return Json(new { success = false, message = "Error confirming job: " + ex.Message });
            }
        }
    }
}
