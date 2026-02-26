using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _configuration;

        // 🔥 Hardcoded Admin (Dev Only)
        private const string DevAdminEmail = "Nutribite123@gmail.com";
        private const string DevAdminPassword = "NutriBite//26";

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
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
            // Minimal server-side validation to mirror client checks
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "All fields are required." });

            var cs = _configuration.GetConnectionString("DBCS");
            if (string.IsNullOrWhiteSpace(cs))
                return Json(new { success = false, message = "Server misconfigured." });

            try
            {
                int newId = 0;

                using (var con = new SqlConnection(cs))
                {
                    con.Open();

                    // Insert and return new user's Id (SCOPE_IDENTITY)
                    using var cmd = new SqlCommand(@"
                        INSERT INTO UserSignup (Name, Email, Password, CreatedAt)
                        VALUES (@n, @e, @p, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);
                    ", con);

                    cmd.Parameters.AddWithValue("@n", name.Trim());
                    cmd.Parameters.AddWithValue("@e", email.Trim());
                    cmd.Parameters.AddWithValue("@p", password);

                    var scalar = cmd.ExecuteScalar();
                    if (scalar != null && int.TryParse(scalar.ToString(), out var parsed))
                        newId = parsed;
                }

                if (newId <= 0)
                    return Json(new { success = false, message = "Unable to create account." });

                // Set session (keep existing session-based auth)
                HttpContext.Session.SetInt32("UserId", newId);
                HttpContext.Session.SetString("UserName", name.Trim());

                // Return redirect to HealthSurvey so client can navigate there
                var redirectUrl = Url.Action("Index", "HealthSurvey") ?? "/HealthSurvey";
                return Json(new { success = true, redirect = redirectUrl });
            }
            catch (Exception ex)
            {
                // Log exception as needed (omitted here for brevity)
                return Json(new { success = false, message = "Server error while creating account." });
            }
        }

        // Simple endpoint used by client JS to decide if user is logged in (session-based)
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
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return Json(new { success = false, message = "Email and password are required." });

            string trimmedEmail = email.Trim();

            // 🔥 ADMIN CHECK (dev)
            if (string.Equals(trimmedEmail, DevAdminEmail, StringComparison.OrdinalIgnoreCase)
                && password == DevAdminPassword)
            {
                HttpContext.Session.SetString("Admin", trimmedEmail);
                HttpContext.Session.SetInt32("UserId", -1);
                HttpContext.Session.SetString("UserName", "Administrator");

                return Json(new { success = true, isAdmin = true });
            }

            // NORMAL USER
            string cs = _configuration.GetConnectionString("DBCS");
            if (string.IsNullOrWhiteSpace(cs))
                return Json(new { success = false, message = "Server misconfigured." });

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                using (SqlCommand cmd = new SqlCommand(
                    "SELECT Id, Name FROM UserSignup WHERE Email = @e AND Password = @p", con))
                {
                    cmd.Parameters.AddWithValue("@e", trimmedEmail);
                    cmd.Parameters.AddWithValue("@p", password);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = Convert.ToInt32(reader["Id"]);
                            string userName = reader["Name"] as string ?? string.Empty;

                            // Set session (app's "signed in" state)
                            HttpContext.Session.SetInt32("UserId", userId);
                            HttpContext.Session.SetString("UserName", userName);

                            // Minimal check for existing HealthSurvey
                            bool requiresSurvey = true;
                            try
                            {
                                using var con2 = new SqlConnection(cs);
                                con2.Open();
                                using var cmd2 = new SqlCommand("SELECT COUNT(1) FROM HealthSurveys WHERE UserId = @u", con2);
                                cmd2.Parameters.AddWithValue("@u", userId);
                                int count = Convert.ToInt32(cmd2.ExecuteScalar());
                                requiresSurvey = (count == 0);
                            }
                            catch
                            {
                                requiresSurvey = false;
                            }

                            return Json(new { success = true, isAdmin = false, requiresSurvey, userName });
                        }
                    }
                }
            }

            return Json(new { success = false, message = "Invalid email or password." });
        }

        // =========================
        // GET: /Auth/Logout
        // =========================
        [HttpGet]
        public IActionResult Logout()
        {
            // Clear session to sign out
            try { HttpContext.Session.Clear(); } catch { }
            return RedirectToAction("Index", "Home");
        }
    }
}