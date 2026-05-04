using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using global::NUTRIBITE.Models;
using global::NUTRIBITE.Services;
using System.Threading.Tasks;

namespace NUTRIBITE.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IEmailService _emailService;

        public AuthController(ApplicationDbContext context, IGoogleAuthService googleAuthService, IEmailService emailService)
        {
            _context = context;
            _googleAuthService = googleAuthService;
            _emailService = emailService;
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

            if (!IsValidEmail(email))
            {
                return Json(new { success = false, message = "Invalid email format." });
            }

            if (!email.Trim().EndsWith(".com", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Email must end with .com." });
            }

            if (!IsStrongPassword(password))
            {
                return Json(new { success = false, message = "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one digit, and one special character." });
            }

            var exists = _context.UserSignups
                .Any(u => u.Email == email.Trim());

            if (exists)
                return Json(new { success = false, message = "Email already exists." });

            var user = new UserSignup
            {
                Name = name.Trim(),
                Email = email.Trim(),
                Password = HashPassword(password), // Hash the password
                Role = "User", // ⭐ DEFAULT ROLE
                CreatedAt = DateTime.Now
            };

            _context.UserSignups.Add(user);
            _context.SaveChanges();

            // Set session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserRole", user.Role);

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
            var role = HttpContext.Session.GetString("UserRole") ?? "User";

            return Json(new { authenticated = true, userName, role });
        }

        // =========================
        // GET: /Auth/Forgot
        // =========================
        [HttpGet]
        public IActionResult Forgot()
        {
            return View();
        }

        // =========================
        // POST: /Auth/ResetPassword
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(string email, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
            {
                return Json(new { success = false, message = "Email and new password are required." });
            }

            var trimmedEmail = email.Trim();
            bool found = false;

            // Check UserSignup
            var user = _context.UserSignups.FirstOrDefault(u => u.Email == trimmedEmail);
            if (user != null)
            {
                user.Password = HashPassword(newPassword);
                found = true;
            }

            // Check VendorSignup
            var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == trimmedEmail);
            if (vendor != null)
            {
                vendor.PasswordHash = HashPassword(newPassword);
                found = true;
            }

            if (!found)
            {
                return Json(new { success = false, message = "Email address not found in our system." });
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "Password reset successfully!", redirect = "/Auth/Login" });
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

            if (!IsValidEmail(email))
            {
                return Json(new { success = false, message = "Invalid email format." });
            }

            // ⭐ ADMIN LOGIN CHECK
            if (email.Trim() == "Nutribite123@gmail.com" && password == "NutriBite//26")
            {
                HttpContext.Session.SetInt32("UserId", 0); // System Admin ID
                HttpContext.Session.SetString("Admin", email.Trim());
                HttpContext.Session.SetString("UserName", "System Admin");
                HttpContext.Session.SetString("UserRole", "Admin");
                return Json(new { success = true, isAdmin = true, userName = "System Admin" });
            }

            var user = _context.UserSignups.FirstOrDefault(u => u.Email == email.Trim());
            
            if (user != null)
            {
                if (user.Password != HashPassword(password))
                {
                    return Json(new { success = false, message = "Password not match." });
                }
                if (user.Status == "Blocked")
                {
                    return Json(new { success = false, message = "Your account has been temporarily blocked by an Administrator." });
                }
            }
            else
            {
                // Fallback for Vendor table if not found in UserSignup
                var vendorOnly = _context.VendorSignups.FirstOrDefault(v => v.Email == email.Trim());
                
                if (vendorOnly != null)
                {
                    if (vendorOnly.PasswordHash != HashPassword(password))
                    {
                        return Json(new { success = false, message = "Password not match." });
                    }
                    if (vendorOnly.IsRejected) return Json(new { success = false, message = "Your vendor account was rejected." });
                    if (!vendorOnly.IsApproved) return Json(new { success = false, message = "Waiting for admin approval." });

                    HttpContext.Session.SetInt32("UserId", vendorOnly.VendorId); // Use VendorId as UserId for session consistency if needed, but better to have separate VendorId
                    HttpContext.Session.SetInt32("VendorId", vendorOnly.VendorId);
                    HttpContext.Session.SetString("VendorEmail", vendorOnly.Email);
                    HttpContext.Session.SetString("UserName", vendorOnly.VendorName);
                    HttpContext.Session.SetString("UserRole", "Vendor");
                    return Json(new { success = true, isVendor = true, userName = vendorOnly.VendorName });
                }
                
                return Json(new { success = false, message = "Email not found." });
            }

            // ⭐ ACCOUNT STATUS CHECK
            if (user.Status == "Deleted")
            {
                return Json(new { success = false, message = "This account has been deleted. Please contact support if this is an error." });
            }

            if (user.Status == "Blocked")
            {
                return Json(new { success = false, message = "Your account has been blocked. Please contact support." });
            }

            // ⭐ VENDOR APPROVAL CHECK
            if (user.Role == "Vendor" && user.Status != "Approved")
            {
                return Json(new { 
                    success = false, 
                    isPendingVendor = true,
                    message = "Your vendor account is pending approval. You can login only after the admin approves your request." 
                });
            }

            // ⭐ STORE ROLE IN SESSION
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "");
            HttpContext.Session.SetString("UserRole", user.Role ?? "User");


            bool requiresSurvey = !_context.HealthSurveys
                .Any(h => h.UserId == user.Id);

            return Json(new
            {
                success = true,
                isAdmin = user.Role == "Admin",
                isVendor = user.Role == "Vendor",
                requiresSurvey,
                userName = user.Name
            });
        }

        // =========================
        // DELETE ACCOUNT (DEPRECATED)
        // =========================
        [HttpGet]
        public IActionResult DeleteAccount()
        {
            return RedirectToAction("MyProfile", "Home");
        }

        // =========================
        // LOGOUT
        // =========================
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Public");
        }

        // =========================
        // GOOGLE AUTH
        // =========================
        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var url = _googleAuthService.GetGoogleLoginUrl();
            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string code, string error)
        {
            if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            {
                TempData["LoginError"] = "Google authentication was cancelled or failed.";
                return RedirectToAction("Login");
            }

            var result = await _googleAuthService.AuthenticateUserAsync(code);

            if (!result.Success)
            {
                TempData["LoginError"] = result.Error;
                return RedirectToAction("Login");
            }

            // ⭐ DETECT DEMO MODE
            bool isDemoMode = code == "demo_code_123";

            var user = _context.UserSignups.FirstOrDefault(u => u.Email == result.Email);

            if (user == null)
            {
                user = new UserSignup
                {
                    Name = result.Name,
                    Email = result.Email,
                    Password = Guid.NewGuid().ToString("N"), // Random password for social users
                    Role = "User",
                    CreatedAt = DateTime.Now,
                    Status = "Approved"
                };
                _context.UserSignups.Add(user);
                await _context.SaveChangesAsync();
            }

            // Set session
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserRole", user.Role);

            TempData["Success"] = isDemoMode 
                ? $"Demo Mode Active: Successfully logged in as '{user.Name}' ({user.Email})."
                : $"Welcome back, {user.Name}!";
            
            return RedirectToAction("Index", "Home");
        }

        // =========================
        // GET DEMO PROFILE (AJAX)
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetDemoProfile()
        {
            var result = await _googleAuthService.AuthenticateUserAsync("demo_code_123");
            if (result.Success)
            {
                return Json(new { 
                    success = true, 
                    email = result.Email, 
                    name = result.Name,
                    picture = result.Picture
                });
            }
            return Json(new { success = false, message = result.Error });
        }

        // ================= PASSWORD HASH =================
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));

                return builder.ToString();
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsStrongPassword(string password)
        {
            // Minimum 8 characters, at least one uppercase, one lowercase, one digit, one special character
            return password.Length >= 8 &&
                   password.Any(char.IsUpper) &&
                   password.Any(char.IsLower) &&
                   password.Any(char.IsDigit) &&
                   password.Any(ch => !char.IsLetterOrDigit(ch));
        }
    }
}