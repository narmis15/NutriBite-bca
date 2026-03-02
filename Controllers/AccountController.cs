using Microsoft.AspNetCore.Mvc;

namespace NUTRIBITE.Controllers
{
    // Small forwarder so public links can use /Account/... while keeping existing AuthController logic.
    // This avoids changing existing Auth views and keeps client code consistent.
    public class AccountController : Controller
    {
        // GET: /Account/Login -> forward to Auth/Login
        [HttpGet]
        public IActionResult Login()
        {
            return RedirectToAction("Login", "Auth");
        }

        // GET: /Account/Register -> forward to Auth/Register
        [HttpGet]
        public IActionResult Register()
        {
            return RedirectToAction("Register", "Auth");
        }
    }
}