using System;
using System.Web;
using System.Web.Mvc;

namespace CallUp.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class AuthorizeRoleAttribute : AuthorizeAttribute
    {
        private readonly string[] _roles;

        public AuthorizeRoleAttribute(params string[] roles)
        {
            _roles = roles;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

            // First use standard authentication check
            if (!httpContext.User.Identity.IsAuthenticated) return false;

            // Check Session Role (Our current system stores it here for legacy support)
            string sessionRole = httpContext.Session["UserRole"]?.ToString();
            
            if (string.IsNullOrEmpty(sessionRole)) return false;

            foreach (var role in _roles)
            {
                if (sessionRole.Equals(role, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // If authenticated but role doesn't match, redirect to Access Denied
            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new RedirectResult("~/Account/Login"); // Or AccessDenied
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }
}
