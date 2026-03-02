using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Filters
{
    // Simple attribute to protect controllers/actions using session-based authentication
    public class SessionAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;
            var uid = http.Session.GetInt32("UserId");
            // If no session value, redirect to Auth/Login
            if (!uid.HasValue)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}