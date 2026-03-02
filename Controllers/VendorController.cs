using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using NUTRIBITE.Models;

namespace NUTRIBITE.Controllers
{
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public VendorController(ApplicationDbContext context,
                                IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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

        // ================= REGISTER =================
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string vendorName, string email, string password)
        {
            var exists = _context.VendorSignups
                .Any(v => v.Email == email);

            if (exists)
            {
                ViewBag.Error = "Email already exists!";
                return View();
            }

            var vendor = new VendorSignup
            {
                VendorName = vendorName,
                Email = email,
                PasswordHash = HashPassword(password),
                IsApproved = false,
                IsRejected = false
            };

            _context.VendorSignups.Add(vendor);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        // ================= LOGIN =================
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var vendor = _context.VendorSignups
                .FirstOrDefault(v => v.Email == email);

            if (vendor == null)
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            if (vendor.IsRejected == true)
            {
                ViewBag.Error = "Your account was rejected by admin.";
                return View();
            }

            if (vendor.IsApproved != true)
            {
                ViewBag.Error = "Your account is waiting for admin approval.";
                return View();
            }

            if (vendor.PasswordHash != HashPassword(password))
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            HttpContext.Session.SetInt32("VendorId", vendor.VendorId);
            HttpContext.Session.SetString("VendorEmail", email);

            return RedirectToAction("Dashboard");
        }

        // ================= AUTH CHECK =================
        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetInt32("VendorId") != null;
        }

        // ================= DASHBOARD =================
        public IActionResult Dashboard()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            int vendorId = HttpContext.Session.GetInt32("VendorId").Value;

            int totalFoods = _context.Foods
                .Count(f => f.VendorId == vendorId);

            ViewBag.TotalFoods = totalFoods;

            return View();
        }

        // ================= ADD FOOD =================
        public IActionResult AddFood()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            return View();
        }

        [HttpPost]
        public IActionResult AddFood(Food model, IFormFile ImageFile)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            int vendorId = HttpContext.Session.GetInt32("VendorId").Value;

            string imagePath = "";

            if (ImageFile != null && ImageFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "Vendorfooduploads");

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                string fileName = Guid.NewGuid().ToString()
                                  + Path.GetExtension(ImageFile.FileName);

                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                imagePath = "/Vendorfooduploads/" + fileName;
            }

            model.ImagePath = imagePath;
            model.VendorId = vendorId;
            model.CreatedAt = DateTime.Now;
            model.Status = "Active";

            _context.Foods.Add(model);
            _context.SaveChanges();

            return RedirectToAction("MyFood");
        }

        // ================= MY FOODS =================
        public IActionResult MyFood()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            int vendorId = HttpContext.Session.GetInt32("VendorId").Value;

            var foods = _context.Foods
                .Where(f => f.VendorId == vendorId)
                .ToList();

            return View(foods);
        }

        // ================= ORDERS =================
        public IActionResult Order()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            return View();
        }

        // ================= PROFILE =================
        public IActionResult Profile()
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            return View();
        }

        // ================= LOGOUT =================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}