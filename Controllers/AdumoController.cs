using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;
using CallUp.Services;

namespace CallUp.Controllers
{
    [Authorize]
    public class AdumoController : CallUpController
    {

        [HttpGet]
        public ActionResult Initialize(int requestId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    // Fetch request and amount from the accepted bid
                    string sql = @"SELECT r.Id, r.Title, b.Amount 
                                 FROM ServiceRequests r
                                 JOIN Bids b ON r.Id = b.RequestId AND b.ProviderId = r.SelectedProviderId
                                 WHERE r.Id = @Id AND b.Status IN ('Accepted', 'Selected')";
                    
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", requestId);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            decimal amount = (decimal)reader["Amount"];
                            string title = reader["Title"].ToString();
                            
                            string merchantId = ConfigurationManager.AppSettings["AdumoMerchantId"];
                            string appId = ConfigurationManager.AppSettings["AdumoApplicationId"];
                            string secret = ConfigurationManager.AppSettings["AdumoSecret"];
                            
                             string mref = "REQ-" + requestId;
                             var rand = new byte[32];
                             using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider()) { rng.GetBytes(rand); }
                             // Ensure jti is Base64Url safe
                             string jti = Convert.ToBase64String(rand).Split('=')[0].Replace('+', '-').Replace('/', '_');

                             var payload = new Dictionary<string, object>
                             {
                                 { "iss", "Dev Center" },
                                 { "cuid", merchantId },
                                 { "auid", appId },
                                 { "amount", amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) },
                                 { "mref", mref },
                                 { "jti", jti },
                                 { "iat", (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds },
                                 { "exp", (long)(DateTime.UtcNow.AddMinutes(15) - new DateTime(1970, 1, 1)).TotalSeconds },
                                 { "notificationURL", Url.Action("Notify", "Adumo", null, Request.Url.Scheme) }
                             };
 
                             string token = AdumoHelper.GenerateJwt(payload, secret);
 
                             ViewBag.MerchantId = merchantId;
                             ViewBag.AppId = appId;
                             ViewBag.Amount = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                             ViewBag.Mref = mref;
                             ViewBag.Token = token;
                             ViewBag.Currency = "ZAR";
                             // Using the documented test puid to ensure session profile load
                             ViewBag.Puid = "c88e2edb-1604-4857-8487-43f0138c33ac";
                            ViewBag.Endpoint = ConfigurationManager.AppSettings["AdumoEndpoint"];
                            ViewBag.SuccessUrl = Url.Action("Success", "Adumo", new { id = requestId }, Request.Url.Scheme);
                            ViewBag.FailUrl = Url.Action("Failed", "Adumo", new { id = requestId }, Request.Url.Scheme);

                            return View();
                        }
                    }
                }
                return RedirectToAction("Customer", "Dashboard");
            }
            catch (Exception)
            {
                return RedirectToAction("Customer", "Dashboard");
            }
        }

        [AllowAnonymous]
        public ActionResult Success(int id)
        {
            try
            {
                // Update status immediately on return to ensure UI is in sync
                UpdatePaymentStatus(id);

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    string sql = "SELECT Title FROM ServiceRequests WHERE Id = @Id";
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    ViewBag.RequestTitle = cmd.ExecuteScalar()?.ToString() ?? "Service Request";
                    ViewBag.RequestId = id;
                }
            }
            catch (Exception ex)
            {
                // Log but still show success page as payment reached here
                System.Diagnostics.Trace.WriteLine("logx: Success Status Update Error: " + ex.Message);
            }
            return View();
        }
 
        [AllowAnonymous]
        public ActionResult Failed(int id)
        {
            ViewBag.RequestId = id;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult Notify(string token)
        {
            try
            {
                // 1. Decode JWT to get mref (REQ-ID)
                var parts = token.Split('.');
                if (parts.Length < 2) return Content("Invalid Token");

                string payloadJson = System.Text.Encoding.UTF8.GetString(ConvertFromBase64Url(parts[1]));
                var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(payloadJson);

                if (payload.ContainsKey("mref") && payload.ContainsKey("status") && payload["status"].ToString() == "1")
                {
                    string mref = payload["mref"].ToString(); // "REQ-123"
                    int requestId = int.Parse(mref.Replace("REQ-", ""));

                    UpdatePaymentStatus(requestId);
                    return Content("OK");
                }
                return Content("Payment Not Successful");
            }
            catch (Exception ex)
            {
                return Content("Error: " + ex.Message);
            }
        }

        private void UpdatePaymentStatus(int requestId)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                conn.Open();
                SqlTransaction trans = conn.BeginTransaction();
                try
                {
                    // Update Request
                    string sqlReq = "UPDATE ServiceRequests SET Status = 'InProgress' WHERE Id = @Id";
                    SqlCommand cmdReq = new SqlCommand(sqlReq, conn, trans);
                    cmdReq.Parameters.AddWithValue("@Id", requestId);
                    cmdReq.ExecuteNonQuery();

                    // Update Bid
                    string sqlBid = "UPDATE Bids SET Status = 'InProgress' WHERE RequestId = @Id AND ProviderId = (SELECT SelectedProviderId FROM ServiceRequests WHERE Id = @Id)";
                    SqlCommand cmdBid = new SqlCommand(sqlBid, conn, trans);
                    cmdBid.Parameters.AddWithValue("@Id", requestId);
                    cmdBid.ExecuteNonQuery();

                    trans.Commit();
                }
                catch
                {
                    trans.Rollback();
                }
            }
        }

        private byte[] ConvertFromBase64Url(string input)
        {
            string output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 0: break;
                case 2: output += "=="; break;
                case 3: output += "="; break;
                default: throw new Exception("Illegal base64url string!");
            }
            return Convert.FromBase64String(output);
        }
    }
}
