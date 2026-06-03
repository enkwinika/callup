using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using CallUp.Models;

namespace CallUp.Services
{
    public interface IAuditService
    {
        void LogActivity(int userId, string action, string details, string ipAddress);
        List<ActivityLog> GetUserLogs(int userId);
    }

    public class AuditService : IAuditService
    {
        private string GetConnectionString() => 
            ConfigurationManager.ConnectionStrings["CallUpContext"].ConnectionString;

        public void LogActivity(int userId, string action, string details, string ipAddress)
        {
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                string sql = @"INSERT INTO ActivityLogs (UserId, Action, Details, IPAddress, CreatedAt)
                               VALUES (@UserId, @Action, @Details, @IPAddress, GETDATE())";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Action", action);
                cmd.Parameters.AddWithValue("@Details", details ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@IPAddress", ipAddress ?? (object)DBNull.Value);
                
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public List<ActivityLog> GetUserLogs(int userId)
        {
            var logs = new List<ActivityLog>();
            using (SqlConnection conn = new SqlConnection(GetConnectionString()))
            {
                string sql = @"SELECT Id, Action, Details, IPAddress, CreatedAt 
                               FROM ActivityLogs 
                               WHERE UserId = @UserId 
                               ORDER BY CreatedAt DESC";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@UserId", userId);
                
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        logs.Add(new ActivityLog
                        {
                            Id = (int)reader["Id"],
                            Action = reader["Action"].ToString(),
                            Details = reader["Details"].ToString(),
                            IPAddress = reader["IPAddress"].ToString(),
                            CreatedAt = (DateTime)reader["CreatedAt"]
                        });
                    }
                }
            }
            return logs;
        }
    }
}
