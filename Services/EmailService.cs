using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Web;

namespace CallUp.Services
{
    public interface IEmailService
    {
        void SendEmail(string to, string subject, string templateName, Dictionary<string, string> placeholders);
        
        // Registration Notifications
        void NotifyAdminOfNewUser(string fullName, string email, string role, string location);
        void NotifyUserOfAccountApproval(string toEmail, string fullName, string role);
        void SendVerificationCode(string toEmail, string fullName, string code);
        void SendPasswordResetCode(string toEmail, string fullName, string token);
        void NotifyAdminOfUserLogin(string userEmail, string fullName, string role, string status);
        
        // Request Notifications
        void NotifyAdminOfNewRequest(string title, string customerName, string location, string description);
        void NotifyCustomerOfRequestLive(string toEmail, string customerName, string title);
        void NotifySupplierOfNewLead(string toEmail, string title, string location, string description);
        
        // Bidding Notifications
        void NotifyCustomerOfNewBid(string toEmail, string customerName, string title, decimal amount, string supplierName);
        void NotifySupplierOfBidSelection(string toEmail, string supplierName, string title, decimal amount);
        
        // Completion & Payment Notifications
        void NotifyCustomerOfJobCompletion(string toEmail, string customerName, string supplierName, string title);
        void NotifySupplierOfPaymentReleased(string toEmail, string supplierName, string title, decimal amount);
    }

    public class EmailService : IEmailService
    {
        private readonly string _host = ConfigurationManager.AppSettings["EmailHost"];
        private readonly int _port = int.Parse(ConfigurationManager.AppSettings["EmailPort"] ?? "587");
        private readonly string _user = ConfigurationManager.AppSettings["EmailUser"];
        private readonly string _pass = ConfigurationManager.AppSettings["EmailPass"];
        private readonly string _from = ConfigurationManager.AppSettings["FromEmail"];
        private readonly string _adminEmail = ConfigurationManager.AppSettings["AdminEmail"] ?? "support@callup.co.za";

        public void SendEmail(string to, string subject, string templateName, Dictionary<string, string> placeholders)
        {
            try
            {
                // Ensure TLS 1.2 is used for SMTP handshake
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // Tls12
                
                // Fallback for untrusted SMTP certificates (Self-signed or private CA)
                ServicePointManager.ServerCertificateValidationCallback = 
                    (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;

                string body = PrepareEmailBody(templateName, placeholders, subject);

                if (string.IsNullOrEmpty(_host) || _host == "YOUR_SMTP_HOST")
                {
                    LogEmail(to, subject, body, "SMTP Config Missing/Default");
                    return;
                }

                using (var message = new MailMessage())
                {
                    message.To.Add(new MailAddress(to));
                    message.From = new MailAddress(_from, "CallUp South Africa");
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = true;

                    using (var client = new SmtpClient(_host, _port))
                    {
                        client.Credentials = new NetworkCredential(_user, _pass);
                        client.EnableSsl = true;
                        client.Send(message);
                    }
                }
                Debug.WriteLine($"[EMAIL] Successfully sent '{subject}' to {to}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMAIL ERROR] Failed to send email to {to}: {ex.Message}");
                // Fallback: Log the email body so it's not lost in Dev
                LogEmail(to, subject, "ERROR: " + ex.Message, "SMTP Failure");
            }
        }

        private string PrepareEmailBody(string templateName, Dictionary<string, string> placeholders, string subject)
        {
            string templatePath = HttpContext.Current.Server.MapPath($"~/Templates/{templateName}.html");
            string layoutPath = HttpContext.Current.Server.MapPath("~/Templates/EmailLayout.html");

            if (!File.Exists(templatePath)) throw new FileNotFoundException("Email template not found: " + templateName);
            if (!File.Exists(layoutPath)) throw new FileNotFoundException("Email layout not found.");

            string templateContent = File.ReadAllText(templatePath);
            string layout = File.ReadAllText(layoutPath);

            // Replace template placeholders
            foreach (var kvp in placeholders)
            {
                templateContent = templateContent.Replace("{{" + kvp.Key + "}}", kvp.Value);
            }

            // Inject into layout
            string finalBody = layout.Replace("{{Content}}", templateContent).Replace("{{Subject}}", subject);

            return finalBody;
        }

        private void LogEmail(string to, string subject, string body, string reason)
        {
            string log = $"\n--- EMAIL LOG ({reason}) ---\nTo: {to}\nSubject: {subject}\nBody Fragment: {body.Substring(0, Math.Min(body.Length, 500))}...\n--------------------------\n";
            Debug.WriteLine(log);
            Console.WriteLine(log);
        }

        // Implementation of notification methods
        public void NotifyAdminOfNewUser(string fullName, string email, string role, string location)
        {
            var placeholders = new Dictionary<string, string> {
                {"FullName", fullName}, {"Email", email}, {"Role", role}, {"Location", location}
            };
            SendEmail(_adminEmail, "Action Required: New User Registration", "Admin_NewUser", placeholders);
        }

        public void NotifyUserOfAccountApproval(string toEmail, string fullName, string role)
        {
            var placeholders = new Dictionary<string, string> {
                {"FullName", fullName}, {"Role", role}
            };
            SendEmail(toEmail, "Welcome to CallUp! Your Account is Active", "User_AccountApproved", placeholders);
        }

        public void SendVerificationCode(string toEmail, string fullName, string code)
        {
            var placeholders = new Dictionary<string, string> {
                {"FullName", fullName}, {"Code", code}
            };
            SendEmail(toEmail, "Verify Your CallUp Account", "User_VerifyEmail", placeholders);
        }

        public void SendPasswordResetCode(string toEmail, string fullName, string token)
        {
            var placeholders = new Dictionary<string, string> {
                {"FullName", fullName}, {"Token", token}
            };
            SendEmail(toEmail, "Reset Your CallUp Password", "User_PasswordReset", placeholders);
        }

        public void NotifyAdminOfNewRequest(string title, string customerName, string location, string description)
        {
            var placeholders = new Dictionary<string, string> {
                {"Title", title}, {"Customer", customerName}, {"Location", location}, {"Description", description}
            };
            SendEmail(_adminEmail, "Action Required: New Service Request for Moderation", "Admin_NewRequest", placeholders);
        }

        public void NotifyCustomerOfRequestLive(string toEmail, string customerName, string title)
        {
            var placeholders = new Dictionary<string, string> {
                {"Customer", customerName}, {"Title", title}
            };
            SendEmail(toEmail, "Your Request is Now Live!", "Customer_RequestLive", placeholders);
        }

        public void NotifySupplierOfNewLead(string toEmail, string title, string location, string description)
        {
            var placeholders = new Dictionary<string, string> {
                {"Title", title}, {"Location", location}, {"Description", description}
            };
            SendEmail(toEmail, "New Lead Alert: " + title, "Supplier_NewLead", placeholders);
        }

        public void NotifyCustomerOfNewBid(string toEmail, string customerName, string title, decimal amount, string supplierName)
        {
            var placeholders = new Dictionary<string, string> {
                {"Customer", customerName}, {"Title", title}, {"Amount", amount.ToString("N2")}, {"Supplier", supplierName}
            };
            SendEmail(toEmail, "New Bid Received for " + title, "Customer_NewBid", placeholders);
        }

        public void NotifySupplierOfBidSelection(string toEmail, string supplierName, string title, decimal amount)
        {
            var placeholders = new Dictionary<string, string> {
                {"Supplier", supplierName}, {"Title", title}, {"Amount", amount.ToString("N2")}
            };
            SendEmail(toEmail, "Congratulations! Your Bid was Selected", "Supplier_BidSelected", placeholders);
        }

        public void NotifyCustomerOfJobCompletion(string toEmail, string customerName, string supplierName, string title)
        {
            var placeholders = new Dictionary<string, string> {
                {"Customer", customerName}, {"Supplier", supplierName}, {"Title", title}, {"CompletedAt", DateTime.Now.ToString("g")}
            };
            SendEmail(toEmail, "Job Completed: " + title, "Customer_JobCompleted", placeholders);
        }

        public void NotifySupplierOfPaymentReleased(string toEmail, string supplierName, string title, decimal amount)
        {
            var placeholders = new Dictionary<string, string> {
                {"Supplier", supplierName}, {"Title", title}, {"Amount", amount.ToString("N2")}, {"PayoutDate", DateTime.Now.ToShortDateString()}
            };
            SendEmail(toEmail, "Payment Released for " + title, "Supplier_PaymentReleased", placeholders);
        }

        public void NotifyAdminOfUserLogin(string userEmail, string fullName, string role, string status)
        {
            var placeholders = new Dictionary<string, string> {
                {"Email", userEmail}, {"FullName", fullName}, {"Role", role}, {"Status", status}, {"LoginTime", DateTime.Now.ToString("F")}
            };
            SendEmail(_adminEmail, $"Security Alert: User Login - {fullName}", "Admin_UserLogin", placeholders);
        }
    }
}
