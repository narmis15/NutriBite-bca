using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using NUTRIBITE.Models;
using NUTRIBITE.Services;

namespace NUTRIBITE.Controllers
{
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IOrderService _orderService;

        public VendorController(ApplicationDbContext context,
                                IWebHostEnvironment environment,
                                IOrderService orderService)
        {
            _context = context;
            _environment = environment;
            _orderService = orderService;
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

        // ================= AUTH CHECK =================
        private int? GetVendorId()
        {
            return HttpContext.Session.GetInt32("VendorId");
        }

        private bool IsLoggedIn()
        {
            return GetVendorId() != null;
        }

        // ================= REGISTER =================
        public IActionResult Register() => View();

        [HttpPost]
        public IActionResult Register(string vendorName, string email, string password)
        {
            if (_context.VendorSignups.Any(v => v.Email == email))
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
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == email);

            if (vendor == null || vendor.PasswordHash != HashPassword(password))
            {
                ViewBag.Error = "Invalid email or password.";
                return View();
            }

            if (vendor.IsRejected == true)
            {
                ViewBag.Error = "Your account was rejected.";
                return View();
            }

            if (vendor.IsApproved != true)
            {
                ViewBag.Error = "Waiting for admin approval.";
                return View();
            }

            HttpContext.Session.SetInt32("VendorId", vendor.VendorId);
            HttpContext.Session.SetString("VendorEmail", email);

            return RedirectToAction("Dashboard");
        }

        // ================= DASHBOARD =================
        public IActionResult Dashboard()
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            var vendorOrderItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Include(oi => oi.Order)
                .Where(oi => oi.Food != null && oi.Food.VendorId == vendorId)
                .ToList();

            int totalFoods = _context.Foods.Count(f => f.VendorId == vendorId);

            int totalOrders = vendorOrderItems
                .Select(oi => oi.OrderId)
                .Distinct()
                .Count();

            decimal totalRevenue = vendorOrderItems
                .Sum(oi => (oi.Quantity ?? 1) * (oi.Food?.Price ?? 0));

            int pendingOrders = _context.OrderTables
                .Count(o => vendorOrderItems.Select(oi => oi.OrderId).Contains(o.OrderId)
                         && o.Status == "Placed");

            // Monthly Revenue
            var monthlyRevenue = vendorOrderItems
                .Where(oi => oi.Order != null && oi.Order.CreatedAt.HasValue
                          && oi.Order.CreatedAt.Value.Year == DateTime.Now.Year)
                .GroupBy(oi => oi.Order.CreatedAt.Value.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Revenue = g.Sum(oi => (oi.Quantity ?? 1) * (oi.Food?.Price ?? 0))
                })
                .OrderBy(x => x.Month)
                .ToList();

            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            decimal[] chartData = new decimal[12];

            for (int i = 0; i < 12; i++)
            {
                var data = monthlyRevenue.FirstOrDefault(x => x.Month == i + 1);
                chartData[i] = data?.Revenue ?? 0;
            }

            var recentOrders = vendorOrderItems
                .OrderByDescending(oi => oi.Order?.CreatedAt)
                .Take(5)
                .Select(oi => new VendorOrderViewModel
                {
                    OrderId = oi.OrderId,
                    CustomerName = oi.Order?.CustomerName,
                    FoodItem = oi.ItemName,
                    Amount = (oi.Quantity ?? 1) * (oi.Food?.Price ?? 0),
                    Status = oi.Order?.Status,
                    Date = oi.Order?.CreatedAt
                })
                .ToList();

            ViewBag.TotalFoods = totalFoods;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.PendingOrders = pendingOrders;
            ViewBag.ChartLabels = months;
            ViewBag.ChartData = chartData;
            ViewBag.RecentOrders = recentOrders;

            return View();
        }

        // ================= ADD FOOD =================
        public IActionResult AddFood()
        {
            if (GetVendorId() == null)
                return RedirectToAction("Login");

            ViewBag.Categories = _context.AddCategories
                .Where(c => c.MealCategory != null)
                .OrderBy(c => c.MealCategory)
                .ToList();

            return View();
        }

        [HttpPost]
        public IActionResult AddFood(Food model, IFormFile ImageFile)
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            string imagePath = "";

            if (ImageFile != null)
            {
                string folder = Path.Combine(_environment.WebRootPath, "Vendorfooduploads");
                Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                string path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                    ImageFile.CopyTo(stream);

                imagePath = "/Vendorfooduploads/" + fileName;
            }

            model.VendorId = vendorId.Value;
            model.ImagePath = imagePath;
            model.CreatedAt = DateTime.Now;
            model.Status = "Active";

            _context.Foods.Add(model);
            _context.SaveChanges();

            return RedirectToAction("MyFood");
        }

        // ================= MY FOOD =================
        public IActionResult MyFood()
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            return View(_context.Foods.Where(f => f.VendorId == vendorId).ToList());
        }

        // ================= ORDERS =================
        public IActionResult Order()
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            var orders = _context.OrderItems
                .Include(oi => oi.Food)
                .Include(oi => oi.Order)
                .Where(oi => oi.Food != null && oi.Food.VendorId == vendorId)
                .Select(oi => new VendorOrderViewModel
                {
                    OrderId = oi.OrderId,
                    CustomerName = oi.Order.CustomerName,
                    FoodItem = oi.ItemName,
                    OrderType = oi.Order.OrderType,
                    Quantity = oi.Quantity,
                    SpecialInstruction = oi.SpecialInstruction,
                    Amount = (oi.Quantity ?? 1) * (oi.Food.Price),
                    Status = oi.Order.Status,
                    Date = oi.Order.CreatedAt
                })
                .OrderByDescending(o => o.Date)
                .ToList();

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            if (GetVendorId() == null)
                return Json(new { success = false });

            var ok = await _orderService.UpdateOrderStatusAsync(orderId, status);
            return Json(new { success = ok });
        }
        public IActionResult Profile()
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            var vendor = _context.VendorSignups.FirstOrDefault(v => v.VendorId == vendorId);
            return View(vendor);
        }

        [HttpPost]
        public IActionResult Profile(string vendorName, string email, string phone, string address, string description, string openingHours, string closingHours, string upiId, IFormFile LogoFile)
        {
            if (!IsLoggedIn())
                return RedirectToAction("Login");

            int vendorId = HttpContext.Session.GetInt32("VendorId").Value;
            var vendor = _context.VendorSignups.FirstOrDefault(v => v.VendorId == vendorId);

            if (vendor != null)
            {
                vendor.VendorName = vendorName;
                vendor.Email = email;
                vendor.Phone = phone;
                vendor.Address = address;
                vendor.Description = description;
                vendor.OpeningHours = openingHours;
                vendor.ClosingHours = closingHours;
                vendor.UpiId = upiId;

                if (LogoFile != null && LogoFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "Vendorlogos");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(LogoFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        LogoFile.CopyTo(stream);
                    }

                    vendor.LogoPath = "/Vendorlogos/" + fileName;
                }

                _context.SaveChanges();
                ViewBag.Success = "Profile updated successfully!";
            }

            return View(vendor);
        }




        // ================= LOGOUT =================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}