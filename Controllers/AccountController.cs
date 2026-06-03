using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Web.Security;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CallUp.Models;

namespace CallUp.Controllers
{
    [Authorize]
    public class AccountController : CallUpController
    {
        private CallUp.Services.IEmailService _emailService = new CallUp.Services.EmailService();
        private CallUp.Services.IAuditService _auditService = new CallUp.Services.AuditService();
        private CallUp.Services.ILogService _logService = new CallUp.Services.LogService();


        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string Email, string Password, bool RememberMe = false, string returnUrl = null)
        {
            string connString = GetConnectionString();
            using (SqlConnection connection = new SqlConnection(connString))
            {
                string sql = "SELECT * FROM Users WHERE Email = @Email";
                SqlCommand cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Email", Email);

                connection.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string storedPassword = reader["Password"]?.ToString();
                        bool isValid = false;
                        bool shouldUpgrade = false;

                        try
                        {
                            if (storedPassword != null && System.Web.Helpers.Crypto.VerifyHashedPassword(storedPassword, Password))
                            {
                                isValid = true;
                            }
                        }
                        catch (FormatException)
                        {
                            // If it's not a valid Base64 hash, check if it's a plain-text match
                            if (storedPassword == Password)
                            {
                                isValid = true;
                                shouldUpgrade = true;
                            }
                        }

                        if (isValid)
                        {
                            // Safely read columns to avoid IndexOutOfRangeException if schema is slightly out of sync
                            string fullName = reader.HasColumn("FullName") ? reader["FullName"]?.ToString() ?? "User" : "User";
                            string userRole = reader.HasColumn("UserRole") ? reader["UserRole"]?.ToString() ?? "Customer" : "Customer";
                            int userId = reader.HasColumn("Id") ? (int)reader["Id"] : 0;
                            bool isVerified = reader.HasColumn("IsVerified") && (reader["IsVerified"] != DBNull.Value && (bool)reader["IsVerified"]);
                            bool isApproved = reader.HasColumn("IsApproved") ? (reader["IsApproved"] != DBNull.Value && (bool)reader["IsApproved"]) : true; // Default to true if column missing for backward compatibility during migration

                            if (!isVerified)
                            {
                                TempData["Message"] = "Please verify your email address before logging in.";
                                TempData["MessageType"] = "warning";
                                return View();
                            }

                            // Note: Removed blocking check for !isApproved to allow users (especially Providers) 
                            // to see their status and dashboard in a limited/pending state.

                            // Auto-upgrade plain-text password to hash
                            if (shouldUpgrade)
                            {
                                string newHash = System.Web.Helpers.Crypto.HashPassword(Password);
                                using (SqlConnection updateConn = new SqlConnection(connString))
                                {
                                    string updateSql = "UPDATE Users SET Password = @NewHash WHERE Id = @Id";
                                    SqlCommand updateCmd = new SqlCommand(updateSql, updateConn);
                                    updateCmd.Parameters.AddWithValue("@NewHash", newHash);
                                    updateCmd.Parameters.AddWithValue("@Id", userId);
                                    updateConn.Open();
                                    updateCmd.ExecuteNonQuery();
                                }
                            }

                            Session["UserId"] = userId;
                            Session["UserRole"] = userRole;
                            Session["FullName"] = fullName;

                            FormsAuthentication.SetAuthCookie(Email, RememberMe);
                            TempData["Message"] = "Welcome back! Successfully logged in.";
                            TempData["MessageType"] = "success";

                            _auditService.LogActivity(userId, "Login", "User logged in successfully (migrated to hash: " + shouldUpgrade + ")", Request.UserHostAddress);

                            // Notify Admin of Login
                            try {
                                _emailService.NotifyAdminOfUserLogin(Email, fullName, userRole, isApproved ? "Active/Approved" : "Pending Approval");
                            } catch { /* Silent fail for notification */ }

                            TempData["logx"] = "[Account/Login] Success for " + Email + ", Role: " + userRole + ". Redirecting to Dashboard/Index";
                            return RedirectToAction("Index", "Dashboard");
                        }
                    }
                }
            }

            TempData["Message"] = "Invalid email or password.";
            TempData["MessageType"] = "error";
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string Email, string Password, string UserRole,
                                     string FirstName, string LastName, string PhoneNumber = null,
                                     string CompanyName = null, string CompanyRegNo = null,
                                     string Location = null, int? CategoryId = null, string OtherCategory = null,
                                     double? Latitude = null, double? Longitude = null)
        {
            if (ModelState.IsValid)
            {
                string connString = GetConnectionString();
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    connection.Open();

                    // Check if user already exists
                    string checkSql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
                    SqlCommand checkCmd = new SqlCommand(checkSql, connection);
                    checkCmd.Parameters.AddWithValue("@Email", Email);
                    int count = (int)checkCmd.ExecuteScalar();

                    if (count > 0)
                    {
                        return Json(new { success = false, message = "This email address is already registered." });
                    }

                    try 
                    {
                        string fullName = FirstName + " " + LastName;
                        string hashedPassword = System.Web.Helpers.Crypto.HashPassword(Password);
                        string verificationCode = new Random().Next(100000, 999999).ToString();

                        // If "Other" (Id 0) is selected, we nullify the ID and save the custom string
                        int? finalCategoryId = (CategoryId == 0) ? (int?)null : CategoryId;
                        string finalCategoryName = (CategoryId == 0) ? OtherCategory : null;

                        // Build INSERT
                        string sql = @"INSERT INTO Users (Email, Password, FullName, UserRole, CompanyName, CompanyRegNo, Location, Latitude, Longitude, CategoryId, Category, PhoneNumber, IsVerified, VerificationCode, VerificationExpiry)
                                     VALUES (@Email, @Password, @FullName, @Role,
                                             @CompanyName, @CompanyRegNo, @Location, @Lat, @Lon, @CatId, @Category, @Phone, 0, @Code, DATEADD(minute, 30, GETDATE()))";

                        sql += "; SELECT SCOPE_IDENTITY();";
                        SqlCommand cmd = new SqlCommand(sql, connection);
                        cmd.Parameters.AddWithValue("@Email", Email);
                        cmd.Parameters.AddWithValue("@Password", hashedPassword);
                        cmd.Parameters.AddWithValue("@FullName", fullName);
                        cmd.Parameters.AddWithValue("@Role", UserRole);
                        cmd.Parameters.AddWithValue("@CompanyName",  string.IsNullOrEmpty(CompanyName)  ? (object)DBNull.Value : CompanyName);
                        cmd.Parameters.AddWithValue("@CompanyRegNo", string.IsNullOrEmpty(CompanyRegNo) ? (object)DBNull.Value : CompanyRegNo);
                        cmd.Parameters.AddWithValue("@Location",     string.IsNullOrEmpty(Location)     ? (object)DBNull.Value : Location);
                        cmd.Parameters.AddWithValue("@Lat",          Latitude.HasValue                  ? (object)Latitude : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Lon",          Longitude.HasValue                 ? (object)Longitude : DBNull.Value);
                        cmd.Parameters.AddWithValue("@CatId",        finalCategoryId.HasValue           ? (object)finalCategoryId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Category",     string.IsNullOrEmpty(finalCategoryName) ? (object)DBNull.Value : finalCategoryName);
                        cmd.Parameters.AddWithValue("@Phone",        string.IsNullOrEmpty(PhoneNumber)  ? (object)DBNull.Value : PhoneNumber);
                        cmd.Parameters.AddWithValue("@Code",         verificationCode);

                        int newUserId = Convert.ToInt32(cmd.ExecuteScalar());
                        Session["UserId"] = newUserId;

                        _auditService.LogActivity(newUserId, "Registration", $"New user registered as {UserRole}. Verification code sent.", Request.UserHostAddress);

                        _emailService.SendVerificationCode(Email, fullName, verificationCode);
                        
                        // Notify Admin (Email)
                        string adminInfo = fullName + (string.IsNullOrEmpty(PhoneNumber) ? "" : " (" + PhoneNumber + ")");
                        _emailService.NotifyAdminOfNewUser(adminInfo, Email, UserRole, Location);

                        // Notify Admin (In-App)
                        try 
                        {
                            string notifySql = @"INSERT INTO Notifications (UserId, Message) 
                                               SELECT Id, @Msg FROM Users WHERE UserRole = 'Admin'";
                            using (SqlCommand notifyCmd = new SqlCommand(notifySql, connection))
                            {
                                string msg = $"New {UserRole} registered: {fullName} ({Email}). Approval required.";
                                notifyCmd.Parameters.AddWithValue("@Msg", msg);
                                notifyCmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception nex)
                        {
                            // Log but don't fail registration
                            _logService.LogException(nex, "Account", "RegisterNotification", newUserId);
                        }

                        Session["UserRole"] = UserRole;
                        Session["FullName"] = fullName;

                        return Json(new { success = true, message = "Registration successful!", redirectUrl = Url.Action("VerifyEmail", new { email = Email }) });
                    }
                    catch (SqlException ex)
                    {
                        return Json(new { success = false, message = "Database Error: " + ex.Message });
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = "Server Error: " + ex.Message });
                    }
                }
            }
            
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = "Validation failed.", errors = errors });
        }

        [AllowAnonymous]
        public ActionResult VerifyEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Register");
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyEmail(string Email, string Code)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Code))
            {
                return Json(new { success = false, message = "Email and verification code are required." });
            }

            try 
            {
                string connString = GetConnectionString();
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    connection.Open();
                    string sql = "SELECT * FROM Users WHERE Email = @Email AND VerificationCode = @Code AND VerificationExpiry > GETDATE()";
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Email", Email);
                    cmd.Parameters.AddWithValue("@Code", Code);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = (int)reader["Id"];
                            string userRole = reader["UserRole"].ToString();
                            string fullName = reader["FullName"].ToString();
                            reader.Close();

                            string updateSql = "UPDATE Users SET IsVerified = 1, VerificationCode = NULL WHERE Id = @Id";
                            SqlCommand updateCmd = new SqlCommand(updateSql, connection);
                            updateCmd.Parameters.AddWithValue("@Id", userId);
                            updateCmd.ExecuteNonQuery();

                            Session["UserId"] = userId;
                            Session["UserRole"] = userRole;
                            Session["FullName"] = fullName;

                            FormsAuthentication.SetAuthCookie(Email, false);
                            _auditService.LogActivity(userId, "Verification", "Email verified successfully.", Request.UserHostAddress);

                            return Json(new { success = true, message = "Email verified successfully!", redirectUrl = Url.Action("Index", "Dashboard") });
                        }
                    }
                }
                return Json(new { success = false, message = "Invalid or expired verification code." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error during verification: " + ex.Message });
            }
        }

        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string Email)
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                TempData["Error"] = "Please enter your email address.";
                return View();
            }

            string connString = GetConnectionString();
            string token = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            using (SqlConnection conn = new SqlConnection(connString))
            {
                conn.Open();
                
                // Get user info for email
                string userSql = "SELECT FullName FROM Users WHERE Email = @Email";
                SqlCommand userCmd = new SqlCommand(userSql, conn);
                userCmd.Parameters.AddWithValue("@Email", Email);
                string fullName = userCmd.ExecuteScalar()?.ToString() ?? "User";

                // Insert token regardless (don't reveal if email exists)
                string sql = @"INSERT INTO PasswordResetTokens (Email, Token, Expiry, Used)
                               VALUES (@Email, @Token, DATEADD(hour, 2, GETDATE()), 0)";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Token", token);
                cmd.ExecuteNonQuery();

                // Send Email
                if (fullName != "User") {
                    _emailService.SendPasswordResetCode(Email, fullName, token);
                }
            }

            ViewBag.Email = Email;
            ViewBag.Token = token;
            return View("ForgotPasswordConfirm");
        }

        [AllowAnonymous]
        public ActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token)) return RedirectToAction("Login");
            ViewBag.Token = token;
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string Email, string Token, string NewPassword, string ConfirmPassword)
        {
            if (NewPassword != ConfirmPassword)
            {
                TempData["Message"] = "Passwords do not match.";
                TempData["MessageType"] = "error";
                ViewBag.Token = Token;
                ViewBag.Email = Email;
                return View();
            }

            string connString = GetConnectionString();
            using (SqlConnection connection = new SqlConnection(connString))
            {
                connection.Open();
                
                // Validate Token
                string sql = "SELECT * FROM PasswordResetTokens WHERE Email = @Email AND Token = @Token AND Used = 0 AND Expiry > GETDATE()";
                SqlCommand cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.Parameters.AddWithValue("@Token", Token);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        reader.Close();
                        
                        // Update Password
                        string hashedPassword = System.Web.Helpers.Crypto.HashPassword(NewPassword);
                        string updateSql = "UPDATE Users SET Password = @Password WHERE Email = @Email";
                        SqlCommand updateCmd = new SqlCommand(updateSql, connection);
                        updateCmd.Parameters.AddWithValue("@Password", hashedPassword);
                        updateCmd.Parameters.AddWithValue("@Email", Email);
                        updateCmd.ExecuteNonQuery();

                        // Mark token as used
                        string markSql = "UPDATE PasswordResetTokens SET Used = 1 WHERE Token = @Token";
                        SqlCommand markCmd = new SqlCommand(markSql, connection);
                        markCmd.Parameters.AddWithValue("@Token", Token);
                        markCmd.ExecuteNonQuery();

                        TempData["Message"] = "Password reset successful! You can now log in.";
                        TempData["MessageType"] = "success";
                        return RedirectToAction("Login");
                    }
                }
            }

            TempData["Message"] = "Invalid or expired token.";
            TempData["MessageType"] = "error";
            ViewBag.Token = Token;
            ViewBag.Email = Email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            int? userId = Session["UserId"] as int?;
            if (userId != null)
                _auditService.LogActivity(userId.Value, "LogOut", "User logged out", Request.UserHostAddress);

            Session.Clear();
            FormsAuthentication.SignOut();
            TempData["Message"] = "You have been logged out.";
            TempData["MessageType"] = "info";
            return RedirectToAction("Index", "Home");
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Customer");
        }
    }

    public static class SqlDataReaderExtensions
    {
        public static T SafeRead<T>(this SqlDataReader reader, string columnName)
        {
            int ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return default(T);
            }
            return (T)reader.GetValue(ordinal);
        }

        public static bool HasColumn(this SqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
