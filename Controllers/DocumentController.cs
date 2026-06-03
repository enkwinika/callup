using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.Mvc;

namespace CallUp.Controllers
{
    [Authorize]
    public class DocumentController : CallUpController
    {

        public ActionResult Invoice(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    string sql = @"SELECT r.Id, r.Title, r.Location, r.CreatedAt, 
                                       c.FullName as CustomerName, c.Email as CustomerEmail,
                                       b.Amount
                                 FROM ServiceRequests r
                                 JOIN Users c ON r.CustomerID = c.Id
                                 JOIN Bids b ON r.Id = b.RequestId AND b.ProviderId = r.SelectedProviderId
                                 WHERE r.Id = @Id AND b.Status IN ('Accepted', 'Selected', 'InProgress', 'Completed')";
                    
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ViewBag.DocType = "Tax Invoice";
                            ViewBag.DocNumber = "INV-" + id.ToString().PadLeft(6, '0');
                            ViewBag.Date = ((DateTime)reader["CreatedAt"]).ToString("dd MMM yyyy");
                            ViewBag.CustomerName = reader["CustomerName"].ToString();
                            ViewBag.CustomerEmail = reader["CustomerEmail"].ToString();
                            ViewBag.JobTitle = reader["Title"].ToString();
                            ViewBag.Location = reader["Location"].ToString();
                            ViewBag.Amount = (decimal)reader["Amount"];
                            return View();
                        }
                    }
                }
                return Content("Document not found.");
            }
            catch (Exception ex)
            {
                return Content("Error: " + ex.Message);
            }
        }

        public ActionResult PurchaseOrder(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    string sql = @"SELECT r.Id, r.Title, r.Location, r.CreatedAt, 
                                       p.FullName as ProviderName, p.Email as ProviderEmail,
                                       p.CompanyName, b.Amount
                                 FROM ServiceRequests r
                                 JOIN Users p ON r.SelectedProviderId = p.Id
                                 JOIN Bids b ON r.Id = b.RequestId AND b.ProviderId = r.SelectedProviderId
                                 WHERE r.Id = @Id AND b.Status IN ('Accepted', 'Selected', 'InProgress', 'Completed')";
                    
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Id", id);
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ViewBag.DocType = "Purchase Order";
                            ViewBag.DocNumber = "PO-" + id.ToString().PadLeft(6, '0');
                            ViewBag.Date = DateTime.Now.ToString("dd MMM yyyy");
                            ViewBag.ProviderName = reader["ProviderName"].ToString();
                            ViewBag.CompanyName = reader["CompanyName"]?.ToString() ?? "Independent Contractor";
                            ViewBag.ProviderEmail = reader["ProviderEmail"].ToString();
                            ViewBag.JobTitle = reader["Title"].ToString();
                            ViewBag.Amount = (decimal)reader["Amount"];
                            return View("Invoice"); // Use same layout
                        }
                    }
                }
                return Content("Document not found.");
            }
            catch (Exception ex)
            {
                return Content("Error: " + ex.Message);
            }
        }
    }
}
