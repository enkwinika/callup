using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace CallUp.Controllers
{
    public class SetupController : CallUpController
    {

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult GetTables()
        {
            try
            {
                var tables = new List<string>();
                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    var schema = conn.GetSchema("Tables");
                    foreach (DataRow row in schema.Rows)
                    {
                        string tableName = row["TABLE_NAME"].ToString();
                        string tableType = row["TABLE_TYPE"].ToString();
                        
                        // Only include base tables, exclude system tables if any
                        if (tableType == "BASE TABLE" && !tableName.StartsWith("__"))
                        {
                            tables.Add(tableName);
                        }
                    }
                }
                return Json(tables.OrderBy(t => t).ToList(), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetTableData(string tableName)
        {
            try
            {
                if (string.IsNullOrEmpty(tableName)) return Json(new { success = false, message = "Table name is required." }, JsonRequestBehavior.AllowGet);

                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    
                    // Validate table name
                    var checkCmd = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName", conn);
                    checkCmd.Parameters.AddWithValue("@tableName", tableName);
                    if ((int)checkCmd.ExecuteScalar() == 0)
                    {
                        return Json(new { success = false, message = "Table not found." }, JsonRequestBehavior.AllowGet);
                    }

                    // Get Column Metadata
                    var columns = new List<object>();
                    var columnNames = new List<string>();
                    
                    var schemaQuery = @"
                        SELECT 
                            c.COLUMN_NAME, 
                            c.DATA_TYPE, 
                            c.IS_NULLABLE,
                            COLUMNPROPERTY(OBJECT_ID(c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') as IsIdentity,
                            (SELECT COUNT(*) FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE k 
                             WHERE k.TABLE_NAME = c.TABLE_NAME AND k.COLUMN_NAME = c.COLUMN_NAME 
                             AND OBJECTPROPERTY(OBJECT_ID(k.CONSTRAINT_NAME), 'IsPrimaryKey') = 1) as IsPK
                        FROM INFORMATION_SCHEMA.COLUMNS c
                        WHERE c.TABLE_NAME = @tableName
                        ORDER BY c.ORDINAL_POSITION";

                    using (var cmd = new SqlCommand(schemaQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@tableName", tableName);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var col = new {
                                    Name = reader["COLUMN_NAME"].ToString(),
                                    Type = reader["DATA_TYPE"].ToString(),
                                    IsNullable = reader["IS_NULLABLE"].ToString() == "YES",
                                    IsIdentity = Convert.ToInt32(reader["IsIdentity"]) == 1,
                                    IsPK = Convert.ToInt32(reader["IsPK"]) > 0
                                };
                                columns.Add(col);
                                columnNames.Add(col.Name);
                            }
                        }
                    }

                    var rows = new List<Dictionary<string, object>>();
                    using (var cmd = new SqlCommand($"SELECT * FROM [{tableName}]", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            foreach (var colName in columnNames)
                            {
                                var val = reader[colName];
                                row[colName] = (val == DBNull.Value) ? null : val;
                            }
                            rows.Add(row);
                        }
                    }

                    return Json(new { success = true, columns = columns, rows = rows }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteRecord(string tableName, string pkColumn, string pkValue)
        {
            try
            {
                if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(pkColumn)) 
                    return Json(new { success = false, message = "Invalid parameters." });

                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    var sql = $"DELETE FROM [{tableName}] WHERE [{pkColumn}] = @pkValue";
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@pkValue", pkValue);
                        cmd.ExecuteNonQuery();
                    }
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveRecord(string tableName, string pkColumn, string pkValue, string jsonData)
        {
            try
            {
                var values = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);
                using (var conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    SqlCommand cmd;

                    if (string.IsNullOrEmpty(pkValue)) // Insert
                    {
                        var cols = string.Join(", ", values.Keys.Select(k => $"[{k}]"));
                        var pars = string.Join(", ", values.Keys.Select(k => $"@{k}"));
                        cmd = new SqlCommand($"INSERT INTO [{tableName}] ({cols}) VALUES ({pars})", conn);
                    }
                    else // Update
                    {
                        var sets = string.Join(", ", values.Keys.Select(k => $"[{k}] = @{k}"));
                        cmd = new SqlCommand($"UPDATE [{tableName}] SET {sets} WHERE [{pkColumn}] = @pkValue", conn);
                        cmd.Parameters.AddWithValue("@pkValue", pkValue);
                    }

                    foreach (var kvp in values)
                    {
                        object val = string.IsNullOrEmpty(kvp.Value) ? (object)DBNull.Value : kvp.Value;
                        cmd.Parameters.AddWithValue($"@{kvp.Key}", val);
                    }

                    cmd.ExecuteNonQuery();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunSetup()
        {
            var logs = new List<string>();
            try
            {
                string scriptPath = Server.MapPath("~/init_db.sql");
                if (!System.IO.File.Exists(scriptPath))
                {
                    return Json(new { success = false, message = "init_db.sql not found in root directory." });
                }

                string script = System.IO.File.ReadAllText(scriptPath);
                
                // Split by GO statements
                string[] commands = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    logs.Add("Connected to production database successfully.");

                    foreach (string commandText in commands)
                    {
                        if (string.IsNullOrWhiteSpace(commandText)) continue;

                        try
                        {
                            using (SqlCommand cmd = new SqlCommand(commandText, conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception ex)
                        {
                            logs.Add("Warning/Error on command: " + ex.Message);
                        }
                    }
                }

                return Json(new { success = true, logs = logs, message = "Setup completed. Check logs for details." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Critical Error: " + ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetDatabase()
        {
            var logs = new List<string>();
            try
            {
                using (SqlConnection conn = new SqlConnection(GetConnectionString()))
                {
                    conn.Open();
                    logs.Add("Nuking all tables and constraints...");

                    // Disable all constraints and drop all tables
                    string dropSql = @"
                        DECLARE @sql NVARCHAR(MAX) = '';
                        SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ' DROP CONSTRAINT ' + QUOTENAME(f.name) + ';'
                        FROM sys.foreign_keys AS f
                        INNER JOIN sys.tables AS t ON f.parent_object_id = t.object_id
                        INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id;

                        SELECT @sql += 'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';'
                        FROM sys.tables AS t
                        INNER JOIN sys.schemas AS s ON t.schema_id = s.schema_id
                        WHERE t.name != '__MigrationHistory';

                        EXEC sp_executesql @sql;";

                    using (SqlCommand cmd = new SqlCommand(dropSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    logs.Add("Database wiped clean.");
                }

                // Now run the normal setup
                var setupResult = RunSetup() as JsonResult;
                return setupResult;
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Reset Failed: " + ex.Message });
            }
        }
    }
}
