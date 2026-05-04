using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NUTRIBITE.Filters
{
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // ✅ Login action ko allow karo
            var action = context.ActionDescriptor.RouteValues["action"];
            var controller = context.ActionDescriptor.RouteValues["controller"];

            if (controller == "Admin" && action == "Login")
            {
                return;
            }

            var admin = context.HttpContext.Session.GetString("Admin");

            if (string.IsNullOrEmpty(admin))
            {
                if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                    context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    context.Result = new JsonResult(new { success = false, message = "Session expired. Please login again." }) { StatusCode = 401 };
                }
                else
                {
                    context.Result = new RedirectToActionResult("Login", "Admin", null);
                }
            }
        }
    }
}





