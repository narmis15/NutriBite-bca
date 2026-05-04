using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using global::NUTRIBITE.Models;
using global::NUTRIBITE.Services;
using global::NUTRIBITE.Filters;
using Microsoft.AspNetCore.Authorization;

namespace NUTRIBITE.Controllers
{
    [VendorAuthorize]
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
        public async Task<IActionResult> GetEarningsData(string period = "monthly", DateTime? refDate = null)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Json(new { success = false, message = "Unauthorized" });

            DateTime referenceDate = refDate ?? DateTime.Today;
            DateTime rangeStart = referenceDate;
            DateTime rangeEnd = referenceDate.AddDays(1);

            var periodLower = period?.ToLowerInvariant() ?? "monthly";
            if (periodLower == "daily") { rangeStart = referenceDate.Date; rangeEnd = rangeStart.AddDays(1); }
            else if (periodLower == "weekly") { rangeStart = referenceDate.AddDays(-(int)referenceDate.DayOfWeek); rangeEnd = rangeStart.AddDays(7); }
            else if (periodLower == "monthly") { rangeStart = new DateTime(referenceDate.Year, referenceDate.Month, 1); rangeEnd = rangeStart.AddMonths(1); }
            else if (periodLower == "yearly") { rangeStart = new DateTime(referenceDate.Year, 1, 1); rangeEnd = rangeStart.AddYears(1); }
            else if (periodLower == "alltime") { rangeStart = DateTime.MinValue; rangeEnd = DateTime.MaxValue; }

            // Use the same robust filter as Dashboard
            var vendorOrderIdsFromItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Where(oi => (oi.Food != null && oi.Food.VendorId == vendorId) || 
                             (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == vendorId)))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToList();

            var vendorOrders = await _context.OrderTables
                .Where(o => (o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId)) && o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd)
                .ToListAsync();

            var payouts = await _context.VendorPayouts
                .Where(p => p.VendorId == vendorId.Value && p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd)
                .ToListAsync();
            
            // Total Sales (Gross) - Excluding cancelled
            decimal totalSales = vendorOrders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount);
            
            // Total Profit (Vendor Share) - Excluding cancelled
            decimal totalProfit = vendorOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));
            
            // Total Commission (Admin Share) - Excluding cancelled
            decimal totalCommission = vendorOrders.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? o.CommissionAmount : (o.TotalAmount * 0.1m));

            // Total Loss (Cancelled Orders Value)
            decimal totalLoss = vendorOrders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount);

            // Total Paid (Received)
            decimal totalPaid = payouts.Where(p => p.Status == PayoutStatus.PaidToVendor).Sum(p => p.Amount);
            
            // Pending Payments
            decimal pendingPayments = totalProfit - totalPaid;

            // Trend Data for Chart
            var trendLabels = new List<string>();
            var revenueTrend = new List<double>();
            var profitTrend = new List<double>();
            var lossTrend = new List<double>();

            if (periodLower == "yearly")
            {
                for (int i = 0; i < 12; i++)
                {
                    var d = new DateTime(referenceDate.Year, 1, 1).AddMonths(i);
                    trendLabels.Add(d.ToString("MMM"));
                    var mOrders = vendorOrders.Where(o => o.CreatedAt.Value.Year == d.Year && o.CreatedAt.Value.Month == d.Month).ToList();
                    revenueTrend.Add((double)mOrders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount));
                    profitTrend.Add((double)mOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    lossTrend.Add((double)mOrders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount));
                }
            }
            else if (periodLower == "monthly")
            {
                int daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
                for (int i = 1; i <= daysInMonth; i++)
                {
                    trendLabels.Add(i.ToString());
                    var dOrders = vendorOrders.Where(o => o.CreatedAt.Value.Year == referenceDate.Year && o.CreatedAt.Value.Month == referenceDate.Month && o.CreatedAt.Value.Day == i).ToList();
                    revenueTrend.Add((double)dOrders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount));
                    profitTrend.Add((double)dOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    lossTrend.Add((double)dOrders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount));
                }
            }
            else if (periodLower == "weekly")
            {
                for (int i = 0; i < 7; i++)
                {
                    var d = rangeStart.AddDays(i);
                    trendLabels.Add(d.ToString("ddd"));
                    var wOrders = vendorOrders.Where(o => o.CreatedAt >= d.Date && o.CreatedAt < d.Date.AddDays(1)).ToList();
                    revenueTrend.Add((double)wOrders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount));
                    profitTrend.Add((double)wOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    lossTrend.Add((double)wOrders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount));
                }
            }
            else if (periodLower == "daily")
            {
                for (int i = 0; i < 24; i++)
                {
                    trendLabels.Add($"{i}:00");
                    var hOrders = vendorOrders.Where(o => o.CreatedAt >= rangeStart.Date.AddHours(i) && o.CreatedAt < rangeStart.Date.AddHours(i + 1)).ToList();
                    revenueTrend.Add((double)hOrders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount));
                    profitTrend.Add((double)hOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    lossTrend.Add((double)hOrders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount));
                }
            }

            return Json(new {
                success = true,
                summary = new {
                    totalEarnings = totalSales,
                    totalProfit = totalProfit,
                    totalCommissionDeducted = totalCommission,
                    totalLoss = totalLoss,
                    totalPaid = totalPaid,
                    pendingPayments = pendingPayments,
                    ordersCount = vendorOrders.Count(o => o.Status != "Cancelled"),
                    cancelledCount = vendorOrders.Count(o => o.Status == "Cancelled")
                },
                charts = new {
                    labels = trendLabels,
                    revenue = revenueTrend,
                    profit = profitTrend,
                    loss = lossTrend
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetPayoutsData(int page = 1, int pageSize = 10, string status = null, string period = "monthly", DateTime? refDate = null)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Json(new { success = false, message = "Unauthorized" });

            DateTime referenceDate = refDate ?? DateTime.Today;
            DateTime rangeStart = referenceDate;
            DateTime rangeEnd = referenceDate.AddDays(1);

            var periodLower = period?.ToLowerInvariant() ?? "monthly";
            if (periodLower == "daily") { rangeStart = referenceDate.Date; rangeEnd = rangeStart.AddDays(1); }
            else if (periodLower == "weekly") { rangeStart = referenceDate.AddDays(-(int)referenceDate.DayOfWeek); rangeEnd = rangeStart.AddDays(7); }
            else if (periodLower == "monthly") { rangeStart = new DateTime(referenceDate.Year, referenceDate.Month, 1); rangeEnd = rangeStart.AddMonths(1); }
            else if (periodLower == "yearly") { rangeStart = new DateTime(referenceDate.Year, 1, 1); rangeEnd = rangeStart.AddYears(1); }
            else if (periodLower == "alltime") { rangeStart = DateTime.MinValue; rangeEnd = DateTime.MaxValue; }

            // 1. Get official payouts
            var payoutQuery = _context.VendorPayouts
                .Where(p => p.VendorId == vendorId.Value && p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd);

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
                .Where(o => (o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId)) && o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var orderPayouts = new List<dynamic>();
            foreach (var o in orders)
            {
                string orderStatus = (o.Status == "Cancelled") ? "Cancelled" : ((o.PaymentStatus != null && o.PaymentStatus.Trim() == "PaidToVendor") ? "PaidToVendor" : "Pending");
                if (orderStatus == "PaidToVendor") continue; // Already summarized in the official Batch Payouts below

                if (!officialPayouts.Any(p => p.OrderId == o.OrderId))
                {
                    if (string.IsNullOrEmpty(status) || status == orderStatus)
                    {
                        orderPayouts.Add(new {
                            id = 0,
                            orderId = o.OrderId,
                            payoutMonth = o.Status == "Cancelled" ? "Order Cancelled" : "Order Settlement",
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

        // ================= AUTH CHECK =================
        private int? GetVendorId()
        {
            return HttpContext.Session.GetInt32("VendorId");
        }

        private bool IsLoggedIn()
        {
            return GetVendorId() != null;
        }

        // ================= DASHBOARD DATA (AJAX) =================
        [HttpGet]
        public async Task<IActionResult> GetDashboardData(string period = "monthly", DateTime? refDate = null)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Json(new { success = false, message = "Unauthorized" });

            DateTime referenceDate = refDate ?? DateTime.Today;
            DateTime rangeStart = referenceDate;
            DateTime rangeEnd = referenceDate.AddDays(1);

            var periodLower = period?.ToLowerInvariant() ?? "monthly";
            if (periodLower == "daily") { rangeStart = referenceDate.Date; rangeEnd = rangeStart.AddDays(1); }
            else if (periodLower == "weekly") { rangeStart = referenceDate.AddDays(-(int)referenceDate.DayOfWeek); rangeEnd = rangeStart.AddDays(7); }
            else if (periodLower == "monthly") { rangeStart = new DateTime(referenceDate.Year, referenceDate.Month, 1); rangeEnd = rangeStart.AddMonths(1); }
            else if (periodLower == "yearly") { rangeStart = new DateTime(referenceDate.Year, 1, 1); rangeEnd = rangeStart.AddYears(1); }
            else if (periodLower == "alltime") { rangeStart = DateTime.MinValue; rangeEnd = DateTime.MaxValue; }

            var vendorOrderIdsFromItems = _context.OrderItems
                .Include(oi => oi.Food)
                .Where(oi => (oi.Food != null && oi.Food.VendorId == vendorId) || 
                             (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == vendorId)))
                .Select(oi => oi.OrderId)
                .Distinct()
                .ToList();

            var vendorOrders = await _context.OrderTables
                .Where(o => (o.VendorId == vendorId || vendorOrderIdsFromItems.Contains(o.OrderId)) && o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd)
                .ToListAsync();

            // Trend Labels
            var labels = new List<string>();
            var revenueData = new List<decimal>();
            var profitData = new List<decimal>();
            var lossData = new List<decimal>();
            var orderData = new List<int>();

            if (periodLower == "yearly")
            {
                for (int i = 0; i < 12; i++)
                {
                    var date = new DateTime(referenceDate.Year, 1, 1).AddMonths(i);
                    labels.Add(date.ToString("MMM"));
                    var orders = vendorOrders.Where(o => o.CreatedAt.Value.Year == date.Year && o.CreatedAt.Value.Month == date.Month && o.Status != "Cancelled");
                    revenueData.Add(orders.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    profitData.Add(orders.Sum(o => (o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)) * 0.4m)); // 40% margin for demo
                    lossData.Add(vendorOrders.Where(o => o.CreatedAt.Value.Year == date.Year && o.CreatedAt.Value.Month == date.Month && o.Status == "Cancelled").Sum(o => o.TotalAmount));
                    orderData.Add(orders.Count());
                }
            }
            else if (periodLower == "monthly")
            {
                int daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
                for (int i = 1; i <= daysInMonth; i++)
                {
                    labels.Add(i.ToString());
                    var orders = vendorOrders.Where(o => o.CreatedAt.Value.Year == referenceDate.Year && o.CreatedAt.Value.Month == referenceDate.Month && o.CreatedAt.Value.Day == i && o.Status != "Cancelled");
                    revenueData.Add(orders.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    profitData.Add(orders.Sum(o => (o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)) * 0.4m));
                    lossData.Add(vendorOrders.Where(o => o.CreatedAt.Value.Year == referenceDate.Year && o.CreatedAt.Value.Month == referenceDate.Month && o.CreatedAt.Value.Day == i && o.Status == "Cancelled").Sum(o => o.TotalAmount));
                    orderData.Add(orders.Count());
                }
            }
            else if (periodLower == "weekly")
            {
                for (int i = 0; i < 7; i++)
                {
                    var date = rangeStart.AddDays(i);
                    labels.Add(date.ToString("ddd"));
                    var orders = vendorOrders.Where(o => o.CreatedAt.Value.Date == date.Date && o.Status != "Cancelled");
                    revenueData.Add(orders.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    profitData.Add(orders.Sum(o => (o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)) * 0.4m));
                    lossData.Add(vendorOrders.Where(o => o.CreatedAt.Value.Date == date.Date && o.Status == "Cancelled").Sum(o => o.TotalAmount));
                    orderData.Add(orders.Count());
                }
            }
            else if (periodLower == "daily")
            {
                for (int i = 0; i < 24; i++)
                {
                    labels.Add($"{i}:00");
                    var orders = vendorOrders.Where(o => o.CreatedAt.Value.Date == rangeStart.Date && o.CreatedAt.Value.Hour == i && o.Status != "Cancelled");
                    revenueData.Add(orders.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                    profitData.Add(orders.Sum(o => (o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)) * 0.4m));
                    lossData.Add(vendorOrders.Where(o => o.CreatedAt.Value.Date == rangeStart.Date && o.CreatedAt.Value.Hour == i && o.Status == "Cancelled").Sum(o => o.TotalAmount));
                    orderData.Add(orders.Count());
                }
            }

            var periodPayouts = await _context.VendorPayouts
                .Where(p => p.VendorId == vendorId.Value && p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd)
                .ToListAsync();
                
            decimal processedEarnings = periodPayouts.Where(p => p.Status == PayoutStatus.PaidToVendor).Sum(p => p.Amount);
            decimal accruedEarnings = vendorOrders.Where(o => o.Status != "Cancelled" && (o.PaymentStatus == null || o.PaymentStatus.Trim() != "PaidToVendor")).Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));
            decimal pendingEarningsCalculated = periodPayouts.Where(p => p.Status == PayoutStatus.Pending).Sum(p => p.Amount) + accruedEarnings;

            return Json(new {
                success = true,
                labels,
                revenue = revenueData,
                profit = profitData,
                loss = lossData,
                orders = orderData,
                summary = new {
                    totalRevenue = vendorOrders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount),
                    netEarnings = processedEarnings,
                    totalProfit = vendorOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)),
                    pendingEarnings = pendingEarningsCalculated,
                    totalOrders = vendorOrders.Count(o => o.Status != "Cancelled")
                }
            });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View();

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Register(string vendorName, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(vendorName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }

            if (_context.VendorSignups.Count() >= 10)
            {
                ViewBag.Error = "We are currently not accepting new vendor applications. (Capacity Reached)";
                return View();
            }

            if (!IsValidEmail(email))
            {
                ViewBag.Error = "Invalid email format.";
                return View();
            }

            if (!email.Trim().EndsWith(".com", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "Email must end with .com.";
                return View();
            }

            if (!IsStrongPassword(password))
            {
                ViewBag.Error = "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one digit, and one special character.";
                return View();
            }

            if (_context.VendorSignups.Any(v => v.Email == email))
            {
                ViewBag.Error = "Email already exists!";
                return View();
            }

            var vendor = new VendorSignup
            {
                VendorName = vendorName,
                Email = email,
                PasswordHash = HashPassword(password), // Hash the password
                IsApproved = false,
                IsRejected = false
            };

            _context.VendorSignups.Add(vendor);
            _context.SaveChanges();

            TempData["VendorSuccess"] = "Your account has been created successfully. It is currently under Admin review. Once your profile is approved, you will receive an email notification and you can then log in.";
            return RedirectToAction("Login");
        }

        // ================= LOGIN =================
        [AllowAnonymous]
        public IActionResult Login() => View();

        [HttpPost]
        [AllowAnonymous]
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Email and password are required.";
                return View();
            }

            if (!IsValidEmail(email))
            {
                ViewBag.Error = "Invalid email format.";
                return View();
            }

            var vendor = _context.VendorSignups.FirstOrDefault(v => v.Email == email);

            if (vendor == null)
            {
                ViewBag.Error = "Email not found.";
                return View();
            }

            if (vendor.PasswordHash != HashPassword(password))
            {
                ViewBag.Error = "Password not match.";
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
        public async Task<IActionResult> Dashboard(string period = "monthly")
        {
            var vendorId = GetVendorId();
            if (vendorId == null)
                return RedirectToAction("Login");

            var vendor = _context.VendorSignups.Find(vendorId);
            ViewBag.BusinessName = vendor?.VendorName ?? "Vendor Dashboard";

            int totalFoods = _context.Foods.Count(f => f.VendorId == vendorId) + 
                             _context.BulkItems.Count(b => b.Id == vendorId); 

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

            int totalOrders = vendorOrders.Count;
            decimal totalRevenue = vendorOrders.Sum(o => o.TotalAmount);
            decimal totalProfit = vendorOrders.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));

            var payouts = await _distributionService.GetVendorPayoutsAsync(vendorId.Value);
            decimal processedEarnings = payouts.Where(p => p.Status == PayoutStatus.PaidToVendor).Sum(p => p.Amount);
            
            decimal netEarnings = processedEarnings; // Net is only what has actually been paid out

            decimal accruedEarnings = vendorOrders
                .Where(o => o.PaymentStatus == null || o.PaymentStatus.Trim() != "PaidToVendor")
                .Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m));
            
            decimal pendingEarnings = payouts.Where(p => p.Status == PayoutStatus.Pending).Sum(p => p.Amount) + accruedEarnings;

            int pendingOrders = vendorOrders.Count(o => o.Status != "Delivered" && o.Status != "Completed" && o.Status != "Picked");

            // Chart Data filtering
            List<string> chartLabels = new List<string>();
            List<decimal> chartData = new List<decimal>();

            if (period == "yearly")
            {
                var currentYear = DateTime.Now.Year;
                for (int i = 0; i < 3; i++)
                {
                    int y = currentYear - 2 + i;
                    chartLabels.Add(y.ToString());
                    chartData.Add(vendorOrders.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Year == y).Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                }
            }
            else if (period == "alltime")
            {
                var grouped = vendorOrders.Where(o => o.CreatedAt.HasValue).GroupBy(x => x.CreatedAt.Value.Year).OrderBy(x => x.Key).ToList();
                foreach (var g in grouped)
                {
                    chartLabels.Add(g.Key.ToString());
                    chartData.Add(g.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                }
                if (!chartLabels.Any())
                {
                    chartLabels.Add(DateTime.Now.Year.ToString());
                    chartData.Add(0);
                }
            }
            else // monthly implicitly
            {
                string[] months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                chartLabels.AddRange(months);
                for (int i = 1; i <= 12; i++)
                {
                    chartData.Add(vendorOrders.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Month == i && o.CreatedAt.Value.Year == DateTime.Now.Year).Sum(o => o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m)));
                }
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
                    FoodItem = "See Details",
                    Amount = o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m),
                    Status = o.Status ?? "Placed",
                    Date = o.CreatedAt
                })
                .ToList();

            ViewBag.TotalFoods = totalFoods;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.TotalRevenue = totalRevenue;     // Gross
            ViewBag.NetEarnings = netEarnings;       // Net
            ViewBag.TotalProfit = totalProfit;       // Total Vendor Profit
            ViewBag.PendingEarnings = pendingEarnings;
            ViewBag.PendingOrders = pendingOrders;
            ViewBag.ChartLabels = chartLabels;
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
            model.Status = "Pending";

            _context.Foods.Add(model);
            _context.SaveChanges();
            TempData["Success"] = "Food item added successfully. It is currently pending Admin approval.";

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

        // ================= UPDATE PASSWORD =================
        [HttpPost]
        public async Task<IActionResult> UpdatePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Json(new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                return Json(new { success = false, message = "All fields are required." });

            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "New password and confirm password do not match." });

            var vendor = await _context.VendorSignups.FindAsync(vendorId.Value);
            if (vendor == null) return Json(new { success = false, message = "Vendor not found." });

            if (vendor.PasswordHash != HashPassword(currentPassword))
                return Json(new { success = false, message = "Current password is incorrect." });

            if (newPassword.Length < 8 || !newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower) || !newPassword.Any(char.IsDigit))
                return Json(new { success = false, message = "Password must be at least 8 chars long with uppercase, lowercase, and a number." });

            vendor.PasswordHash = HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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