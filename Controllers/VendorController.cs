using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using global::NUTRIBITE.Models;
using global::NUTRIBITE.Services;

namespace NUTRIBITE.Controllers
{
    public partial class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IOrderService _orderService;

        private readonly IPaymentDistributionService _distributionService;

        public VendorController(ApplicationDbContext context,
                                IWebHostEnvironment environment,
                                IOrderService orderService,
                                IPaymentDistributionService distributionService)
        {
            _context = context;
            _environment = environment;
            _orderService = orderService;
            _distributionService = distributionService;
        }

        [HttpGet]
        public async Task<IActionResult> GetEarningsData()
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Json(new { success = false, message = "Unauthorized" });

            // Use the same robust filter as Dashboard
            var vendorOrderIdsFromItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Where(oi => (oi.Food != null && oi.Food.VendorId == vendorId) || 
                             (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == vendorId)))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToList();

            var vendorOrders = _context.OrderTables
                .Where(o => (o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId)) && o.Status != "Cancelled")
                .ToList();

            var payouts = await _distributionService.GetVendorPayoutsAsync(vendorId.Value);
            
            // Total Sales (Gross)
            decimal totalSales = vendorOrders.Sum(o => o.TotalAmount);
            
            // Total Paid (Received)
            decimal totalPaid = vendorOrders.Where(o => o.PaymentStatus != null && o.PaymentStatus.Trim() == "PaidToVendor").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m))
                               + payouts.Where(p => p.Status == PayoutStatus.PaidToVendor).Sum(p => p.Amount);
            
            // Total Commission
            decimal totalCommission = vendorOrders.Sum(o => o.CommissionAmount > 0 ? o.CommissionAmount : (o.TotalAmount * 0.1m));

            // Pending Payments
            decimal pendingPayments = vendorOrders.Where(o => o.PaymentStatus == null || o.PaymentStatus.Trim() != "PaidToVendor").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m))
                                     + payouts.Where(p => p.Status == PayoutStatus.Pending).Sum(p => p.Amount);

            return Json(new {
                totalEarnings = totalSales,
                totalPaid = totalPaid,
                totalCommissionDeducted = totalCommission,
                pendingPayments = pendingPayments
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetPayoutsData(int page = 1, int pageSize = 10, string status = null)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Json(new { success = false, message = "Unauthorized" });

            // 1. Get official payouts
            var payoutQuery = _context.VendorPayouts
                .Where(p => p.VendorId == vendorId.Value);

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<PayoutStatus>(status, out var payoutStatus))
                    payoutQuery = payoutQuery.Where(p => p.Status == payoutStatus);
            }

            var officialPayouts = await payoutQuery
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // 2. Get orders using the robust filter
            var vendorOrderIdsFromItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Where(oi => (oi.Food != null && oi.Food.VendorId == vendorId) || 
                             (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == vendorId)))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToList();

            var orders = await _context.OrderTables
                .Where(o => (o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId)) && o.Status != "Cancelled")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var orderPayouts = new List<dynamic>();
            foreach (var o in orders)
            {
                if (!officialPayouts.Any(p => p.OrderId == o.OrderId))
                {
                    string orderStatus = (o.PaymentStatus != null && o.PaymentStatus.Trim() == "PaidToVendor") ? "PaidToVendor" : "Pending";
                    if (string.IsNullOrEmpty(status) || status == orderStatus)
                    {
                        orderPayouts.Add(new {
                            id = 0,
                            orderId = o.OrderId,
                            payoutMonth = "Order Settlement",
                            orderDate = o.CreatedAt,
                            totalSales = o.TotalAmount,
                            commissionDeducted = o.CommissionAmount > 0 ? o.CommissionAmount : (o.TotalAmount * 0.1m),
                            amount = o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m),
                            status = orderStatus,
                            updatedAt = o.UpdatedAt ?? o.CreatedAt ?? DateTime.Now
                        });
                    }
                }
            }

            // Combine and paginate
            var allItems = orderPayouts.Concat(officialPayouts.Select(p => (dynamic)new {
                id = p.Id,
                orderId = (int?)null,
                payoutMonth = p.PayoutMonth,
                orderDate = (DateTime?)null,
                totalSales = p.TotalSales,
                commissionDeducted = p.CommissionDeducted,
                amount = p.Amount,
                status = p.Status.ToString(),
                updatedAt = p.UpdatedAt
            })).OrderByDescending(x => x.updatedAt).ToList();

            var totalItems = allItems.Count;
            var items = allItems
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Json(new {
                items,
                totalItems,
                page,
                pageSize
            });
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
                PasswordHash = password, // ⭐ UPDATED: Storing plain text for testing
                IsApproved = false,
                IsRejected = false
            };

            _context.VendorSignups.Add(vendor);
            _context.SaveChanges();

            TempData["VendorSuccess"] = "Your account has been created successfully. It is currently under admin review. You will be able to access full features once your account is verified.";
            return RedirectToAction("Login");
        }

        // ================= LOGIN =================
        public IActionResult Login() => View();

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == email);

            if (vendor == null || vendor.PasswordHash != password)
            {
                ViewBag.Error = "Invalid email or password. Please try again.";
                return View();
            }

            if (vendor.IsRejected == true)
            {
                ViewBag.Error = "Your application has been reviewed and unfortunately was not approved at this time.";
                return View();
            }

            if (vendor.IsApproved != true)
            {
                ViewBag.Error = "Your account is currently pending admin approval. Please wait for up to 24 hours for our team to review and verify your business details.";
                return View();
            }

            HttpContext.Session.SetInt32("VendorId", vendor.VendorId);
            HttpContext.Session.SetString("VendorEmail", email);

            return RedirectToAction("Dashboard");
        }

        private static readonly string[] SpecifiedMealCategories = {
            "Low Calorie Meal", "Protein Meal", "Rice Combo", "Salads",
            "Classical Thali", "Comfort Thali", "Deluxe Thali",
            "Jain Thali", "Special Thali", "Standard Thali"
        };

        // ================= DASHBOARD =================
        public async Task<IActionResult> Dashboard()
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            var vendor = _context.VendorSignups.Find(vendorId);
            ViewBag.BusinessName = vendor?.VendorName ?? "Vendor Dashboard";

            // Total Foods: Count both regular food items and bulk items for this vendor
            int totalFoods = _context.Foods.Count(f => f.VendorId == vendorId) + 
                             _context.BulkItems.Count(b => b.Id == vendorId); // Fixed logic

            // GET ALL ORDERS FOR THIS VENDOR
            // Inclusive logic: by VendorId on OrderTable OR by items in OrderItems
            var vendorOrderIdsFromItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Where(oi => (oi.Food != null && oi.Food.VendorId == vendorId) || 
                             (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == vendorId)))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToList();

            var vendorOrders = _context.OrderTables
                .Where(o => (o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId)) && o.Status != "Cancelled")
                .ToList();

            // Total Orders for this vendor
            int totalOrders = vendorOrders.Count;

            // Total Gross Revenue
            decimal totalRevenue = vendorOrders.Sum(o => o.TotalAmount);

            // Total Received: Orders marked PaidToVendor + official payouts that are Paid
            var payouts = await _distributionService.GetVendorPayoutsAsync(vendorId.Value);
            decimal processedEarnings = payouts.Where(p => p.Status == PayoutStatus.PaidToVendor).Sum(p => p.Amount);
            
            decimal paidOrdersEarnings = vendorOrders
                .Where(o => o.PaymentStatus != null && o.PaymentStatus.Trim() == "PaidToVendor")
                .Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));
            
            decimal netEarnings = processedEarnings + paidOrdersEarnings;

            // Pending Payments: Orders NOT marked PaidToVendor + payouts that are Pending
            decimal accruedEarnings = vendorOrders
                .Where(o => o.PaymentStatus == null || o.PaymentStatus.Trim() != "PaidToVendor")
                .Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));
            
            decimal pendingEarnings = payouts.Where(p => p.Status == PayoutStatus.Pending).Sum(p => p.Amount) + accruedEarnings;

            // Pending Orders: Orders that are not yet Delivered/Completed
            int pendingOrders = vendorOrders.Count(o => o.Status != "Delivered" && o.Status != "Completed" && o.Status != "Picked");

            // Monthly Net Earnings for chart (using revenue from OrderTables)
            string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            decimal[] chartData = new decimal[12];

            for (int i = 0; i < 12; i++)
            {
                // Show monthly revenue based on orders for this year
                var monthlyRevenue = vendorOrders
                    .Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Month == i + 1 && o.CreatedAt.Value.Year == DateTime.Now.Year)
                    .Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));
                
                chartData[i] = monthlyRevenue;
            }

            // Recent Orders for this vendor
            var recentOrders = vendorOrders
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new VendorOrderViewModel
                {
                    OrderId = o.OrderId,
                    CustomerName = o.CustomerName ?? "Guest",
                    CustomerPhone = o.CustomerPhone,
                    DeliveryAddress = o.DeliveryAddress,
                    Amount = o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m),
                    Status = o.Status ?? "Placed",
                    Date = o.CreatedAt
                })
                .ToList();

            ViewBag.TotalFoods = totalFoods;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalRevenue = totalRevenue;     // Gross
            ViewBag.NetEarnings = netEarnings;       // Net
            ViewBag.PendingEarnings = pendingEarnings;
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
                .Where(c => c.MealCategory != null && SpecifiedMealCategories.Contains(c.MealCategory))
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

            // Role-based validation: check if Category belongs to specified list
            var category = _context.AddCategories.Find(model.CategoryId);
            if (category == null || string.IsNullOrEmpty(category.MealCategory) || !SpecifiedMealCategories.Contains(category.MealCategory))
            {
                ViewBag.Error = "Invalid or restricted category selection.";
                ViewBag.Categories = _context.AddCategories
                    .Where(c => c.MealCategory != null && SpecifiedMealCategories.Contains(c.MealCategory))
                    .OrderBy(c => c.MealCategory)
                    .ToList();
                return View(model);
            }

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

            var foods = _context.Foods
                .Include(f => f.Nutritionist)
                .Where(f => f.VendorId == vendorId)
                .ToList();

            return View(foods);
        }

        [HttpPost]
        public IActionResult DeleteFood(int id)
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return Json(new { success = false, message = "Unauthorized" });

            var food = _context.Foods.FirstOrDefault(f => f.Id == id && f.VendorId == vendorId);
            if (food != null)
            {
                _context.Foods.Remove(food);
                _context.SaveChanges();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Food not found" });
        }

        // ================= ORDERS =================
        public IActionResult Order()
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            // Filter all orders where at least one item belongs to this vendor
            var vendorOrderIdsFromItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Where(oi => (oi.Food != null && oi.Food.VendorId == vendorId) || 
                             (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == vendorId)))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToList();

            var orders = _context.OrderTables
                .Include(o => o.OrderItems)
                .Where(o => o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId))
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var viewModel = orders.Select(o => new VendorOrderViewModel
            {
                OrderId = o.OrderId,
                CustomerName = o.CustomerName ?? "Guest",
                CustomerPhone = o.CustomerPhone,
                DeliveryAddress = o.DeliveryAddress,
                OrderType = o.OrderType ?? "Delivery",
                // For the list view, we show the main item name or a summary
                FoodItem = o.OrderItems.Any() ? 
                           (o.OrderItems.First().ItemName + (o.OrderItems.Count > 1 ? $" (+{o.OrderItems.Count - 1} more)" : "")) : 
                           "General Order",
                Quantity = o.OrderItems.Sum(oi => oi.Quantity),
                Amount = o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m),
                Status = o.Status ?? "Placed",
                Date = o.CreatedAt
            }).ToList();

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            if (GetVendorId() == null)
                return Json(new { success = false });

            var ok = await _orderService.UpdateOrderStatusAsync(orderId, status);
            return Json(new { success = ok });
        }

        public IActionResult Earnings()
        {
            if (GetVendorId() == null)
                return RedirectToAction("Login");

            return View();
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

        // ================= EDIT FOOD =================
        [HttpGet]
        public IActionResult EditFood(int id)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return RedirectToAction("Login");

            var food = _context.Foods.FirstOrDefault(f => f.Id == id && f.VendorId == vendorId);
            if (food == null) return NotFound();

            return View(food);
        }

        [HttpPost]
        public async Task<IActionResult> EditFood(int id, Food model, IFormFile? ProductPic)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return RedirectToAction("Login");

            var food = _context.Foods.FirstOrDefault(f => f.Id == id && f.VendorId == vendorId);
            if (food == null) return NotFound();

            food.Name = model.Name;
            food.Description = model.Description;
            food.Price = model.Price;
            food.Calories = model.Calories;
            food.Status = model.Status;
            food.FoodType = model.FoodType;

            if (ProductPic != null && ProductPic.Length > 0)
            {
                var uniqueName = Guid.NewGuid().ToString() + Path.GetExtension(ProductPic.FileName);
                var path = Path.Combine(_environment.WebRootPath, "images/foods", uniqueName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await ProductPic.CopyToAsync(stream);
                }
                food.ImagePath = "/images/foods/" + uniqueName;
            }

            _context.SaveChanges();
            TempData["Success"] = "Food updated successfully.";
            return RedirectToAction("MyFood");
        }

        // ================= SUBSCRIPTIONS =================
        public async Task<IActionResult> Subscriptions()
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return RedirectToAction("Login");

            var subscriptions = await _context.Subscriptions
                .Include(s => s.Food)
                .Include(s => s.User)
                .Where(s => s.VendorId == vendorId || (s.Food != null && s.Food.VendorId == vendorId))
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            return View(subscriptions);
        }
    }
}