using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // GET: /Auth/Login
        // =========================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // =========================
        // GET: /Auth/Register
        // =========================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // =========================
        // POST: /Auth/Register
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string name, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "All fields are required." });
            }

            var exists = _context.UserSignups
                .Any(u => u.Email == email.Trim());

            if (exists)
                return Json(new { success = false, message = "Email already exists." });

            var user = new UserSignup
            {
                Name = name.Trim(),
                Email = email.Trim(),
                Password = password,   // ⚠ Later we can hash this
                CreatedAt = DateTime.Now
            };

            _context.UserSignups.Add(user);
            _context.SaveChanges();

            // Set session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);

            var redirectUrl = Url.Action("Index", "HealthSurvey") ?? "/HealthSurvey";
            return Json(new { success = true, redirect = redirectUrl });
        }

        // =========================
        // CHECK AUTH (AJAX)
        // =========================
        [HttpGet]
        public IActionResult IsAuthenticated()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return Json(new { authenticated = false });

            var userName = HttpContext.Session.GetString("UserName") ?? "";

            return Json(new { authenticated = true, userName });
        }

        // =========================
        // POST: /Auth/Login
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return Json(new { success = false, message = "Email and password are required." });
            }

            var user = _context.UserSignups
                .FirstOrDefault(u =>
                    u.Email == email.Trim() &&
                    u.Password == password);

            if (user == null)
                return Json(new { success = false, message = "Invalid email or password." });

            // Set session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "");

            // Check if health survey exists
            bool requiresSurvey = !_context.HealthSurveys
                .Any(h => h.UserId == user.Id);

            return Json(new
            {
                success = true,
                isAdmin = false,
                requiresSurvey,
                userName = user.Name
            });
        }

        // =========================
        // LOGOUT
        // =========================
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}