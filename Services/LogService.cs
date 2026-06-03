using System;
using System.Configuration;
using System.Data.SqlClient;

namespace CallUp.Services
{
    public interface ILogService
    {
        void LogException(Exception ex, string controller, string action, int? userId = null);
    }

    public class LogService : ILogService
    {
        private string GetConnectionString() =>
            ConfigurationManager.ConnectionStrings["CallUpContext"].ConnectionString;

        public void LogException(Exception ex, string controller, string action, int? userId = null)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    string sql = @"INSERT INTO ErrorLogs (Message, StackTrace, Controller, Action, UserId, CreatedAt) 
                                 VALUES (@Msg, @Stack, @Ctrl, @Act, @User, GETDATE())";
                    
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@Msg", ex.Message ?? "No message");
                    cmd.Parameters.AddWithValue("@Stack", ex.StackTrace ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Ctrl", controller ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Act", action ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@User", userId ?? (object)DBNull.Value);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                // Fail silently to avoid infinite error loops
            }
        }
    }
}
