using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NUTRIBITE.Models;

using NUTRIBITE.Models; // Ensure ErrorViewModel is defined in this namespace


namespace NUTRIBITE.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private const string AdminEmail = "Nutribite123@gmail.com";
        private const string AdminPassword = "NutriBite//26";

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult About()
        {
            return View();
        }

        // GET: /Home/Login
        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        // POST: /Home/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            // Basic server-side validation
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Trim email for consistent comparison
            var email = (model.Email ?? string.Empty).Trim();

            // Hardcoded admin check (no Identity / DB)
            if (string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase)
                && string.Equals(model.Password ?? string.Empty, AdminPassword, StringComparison.Ordinal))
            {
                _logger.LogInformation("Admin login succeeded for {Email}", email);

                // Successful login: redirect to admin dashboard
                return Redirect("/Admin/Dashboard");
            }

            // Failed login
            _logger.LogWarning("Invalid login attempt for {Email}", email);
            ModelState.AddModelError(string.Empty, "Invalid email or password");
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}