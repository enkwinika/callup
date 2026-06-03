using System;
using System.Web.Mvc;
using System.Data.SqlClient;
using System.ComponentModel;
using CallUp.Helpers;

namespace CallUp.Controllers
{
    public abstract class CallUpController : Controller
    {
        protected string GetConnectionString() => DbConfig.GetConnectionString();

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled) return;

            var ex = filterContext.Exception;

            // Check if it's a database timeout or connectivity error
            bool isDbError = ex is SqlException || 
                             ex is Win32Exception || 
                             (ex.InnerException != null && (ex.InnerException is SqlException || ex.InnerException is Win32Exception));

            if (isDbError)
            {
                // Specifically detect the "Wait operation timed out" or similar connectivity issues
                string message = "Database connection timed out or is unreachable. Please verify you are on the correct network or try again later.";
                
                TempData["Message"] = message;
                TempData["MessageType"] = "error";

                filterContext.ExceptionHandled = true;

                // For AJAX requests, return JSON
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = Json(new { success = false, message = message }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    // Redirect back to the previous page or Home if not available
                    string redirectUrl = filterContext.HttpContext.Request.UrlReferrer?.ToString() ?? "/";
                    filterContext.Result = Redirect(redirectUrl);
                }
            }

            base.OnException(filterContext);
        }

        protected bool IsValidImage(System.Web.HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0) return false;
            
            // Validate size (e.g. max 5MB)
            if (file.ContentLength > 5 * 1024 * 1024) return false;

            // Validate extension
            string ext = System.IO.Path.GetExtension(file.FileName).ToLower();
            string[] allowed = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            if (!System.Linq.Enumerable.Contains(allowed, ext)) return false;

            // Validate MIME type
            string mime = file.ContentType.ToLower();
            string[] allowedMime = { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!System.Linq.Enumerable.Contains(allowedMime, mime)) return false;

            return true;
        }

    }
}
