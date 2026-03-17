using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using NutriBite.Filters;
using NUTRIBITE.Models;
using NUTRIBITE.Services;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace NUTRIBITE.Controllers
{
    public partial class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IOrderService _orderService;

        // ApplicationDbContext injected
        private readonly ApplicationDbContext _context;

        public AdminController(IConfiguration configuration, IOrderService orderService, ApplicationDbContext context)
        {
            _configuration = configuration;
            _orderService = orderService;
            _context = context;
        }

        // 🔓 PUBLIC
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 🔓 PUBLIC
        // POST: /Admin/Login
        [HttpPost]
        public IActionResult Login(string UserId, string email, string Password)
        {
            // Accept either form field "UserId" (Admin view) or "email" (other forms)
            var id = !string.IsNullOrWhiteSpace(UserId) ? UserId.Trim() : (email ?? "").Trim();

            if (!string.IsNullOrEmpty(id) &&
                id.Equals("Nutribite123@gmail.com", System.StringComparison.OrdinalIgnoreCase) &&
                Password == "NutriBite//26")
            {
                HttpContext.Session.SetString("Admin", id);
                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid email or Password";
            return View();
        }


        // 🔒 PROTECTED (ONLY THIS)
        [AdminAuthorize]
        public IActionResult Dashboard()
        {
            LoadDashboardCounts();
            return View();
        }

        // 🔒 Admin only
        [AdminAuthorize]
        public IActionResult ManageVendor()
        {
            var vendors = _context.VendorSignups
                .OrderByDescending(v => v.CreatedAt)
                .ToList();

            return View(vendors);
        }
        [AdminAuthorize]
        [HttpGet]
        public IActionResult NewVendorRequest()
        {
            var vendors = _context.VendorSignups
                .Where(v => v.IsApproved == false && v.IsRejected == false)
                .OrderBy(v => v.CreatedAt)
                .ToList();

            return View(vendors);
        }

        // 🔒 Admin only
        [AdminAuthorize]
        [HttpGet]
        public IActionResult AddFoodCategory()
        {
            return View();
        }

        // 🔒 Admin only - Add Food Category (POST) - uses EF Core
        [AdminAuthorize]
        [HttpPost]
        public IActionResult AddFoodCategory(
            string ProductCategory,
            string CustomProductCategory,
            string MealCategory,
            string CustomMealCategory,
            IFormFile CategoryImage)
        {
            string finalProductCategory =
                ProductCategory == "Other" ? CustomProductCategory : ProductCategory;

            string finalMealCategory =
                MealCategory == "Other" ? CustomMealCategory : MealCategory;

            // IMAGE UPLOAD
            string? imagePath = null;
            if (CategoryImage != null)
            {
                string folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");
                Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(CategoryImage.FileName);
                string fullPath = Path.Combine(folder, fileName);

                using var stream = new FileStream(fullPath, FileMode.Create);
                CategoryImage.CopyTo(stream);

                imagePath = "/images/" + fileName;
            }

            try
            {
                var cat = new AddCategory
                {
                    ProductCategory = finalProductCategory ?? "",
                    ProductPic = "", // optional - keep blank unless you set ProductPic
                    MealCategory = finalMealCategory,
                    ImagePath = imagePath,
                    CreatedAt = DateTime.Now
                };

                _context.AddCategories.Add(cat);
                _context.SaveChanges();

                ViewBag.Success = "Food category added successfully";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred while adding the category: " + ex.Message;
            }

            return View();
        }

        // 🔒 Admin only - Add Coupon (GET)
        [AdminAuthorize]
        [HttpGet]
        public IActionResult AddCoupon()
        {
            return View();
        }

        // 🔒 Admin only - Add Coupon (POST) - uses EF Core
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCoupon(
            string CouponCode,
            decimal Discount,
            DateTime StartDate,
            DateTime EndDate)
        {
            // Server-side validation
            if (string.IsNullOrWhiteSpace(CouponCode))
                ModelState.AddModelError(nameof(CouponCode), "Coupon code is required.");

            if (Discount < 0 || Discount > 100)
                ModelState.AddModelError(nameof(Discount), "Discount must be between 0 and 100.");

            if (EndDate < StartDate)
                ModelState.AddModelError(nameof(EndDate), "End date must be on or after start date.");

            if (!ModelState.IsValid)
                return View();

            try
            {
                // prevent duplicate coupon codes
                bool exists = _context.Coupons.Any(c => c.Code == CouponCode);
                if (exists)
                {
                    ModelState.AddModelError(nameof(CouponCode), "A coupon with this code already exists.");
                    return View();
                }

                var coupon = new Coupon
                {
                    Code = CouponCode,
                    Discount = (int)Math.Round(Discount),
                    Startdate = StartDate,
                    Validtill = EndDate
                };

                _context.Coupons.Add(coupon);
                _context.SaveChanges();

                ViewBag.Success = "Coupon added successfully";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred while adding the coupon: " + ex.Message;
            }

            return View();
        }

        // View categories using EF Core
        public IActionResult ViewCategory(string sortOrder)
        {
            ViewBag.IdSort = sortOrder == "id" ? "id_desc" : "id";
            ViewBag.CategorySort = sortOrder == "cat" ? "cat_desc" : "cat";

            var query = _context.AddCategories.AsQueryable();

            // Exclude null/NA product categories (match previous behavior)
            query = query.Where(a => a.ProductCategory != null && a.ProductCategory != "NA");

            if (sortOrder == "id")
                query = query.OrderBy(a => a.Cid);
            else if (sortOrder == "id_desc")
                query = query.OrderByDescending(a => a.Cid);
            else if (sortOrder == "cat")
                query = query.OrderBy(a => a.ProductCategory);
            else if (sortOrder == "cat_desc")
                query = query.OrderByDescending(a => a.ProductCategory);
            else
                query = query.OrderBy(a => a.Cid);

            var list = query.ToList();
            return View(list);
        }

        // View meal categories using EF Core
        public IActionResult ViewMealCategory(string sortOrder)
        {
            ViewBag.IdSort = sortOrder == "id" ? "id_desc" : "id";
            ViewBag.MealSort = sortOrder == "meal" ? "meal_desc" : "meal";

            var query = _context.AddCategories
                        .Where(a => a.MealCategory != null);

            if (sortOrder == "id")
                query = query.OrderBy(a => a.Cid);
            else if (sortOrder == "id_desc")
                query = query.OrderByDescending(a => a.Cid);
            else if (sortOrder == "meal")
                query = query.OrderBy(a => a.MealCategory);
            else if (sortOrder == "meal_desc")
                query = query.OrderByDescending(a => a.MealCategory);

            var list = query.ToList();
            return View(list);
        }



        // 🔓 PUBLIC
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Updated: LoadDashboardCounts now uses EF Core (falls back to SQL helper on error)
        private void LoadDashboardCounts()
        {
            try
            {
                var now = DateTime.UtcNow;
                var sevenDaysAgo = now.AddDays(-7);
                var firstOfMonth = new DateTime(now.Year, now.Month, 1);

                ViewBag.Users = _context.UserSignups.Count();
                ViewBag.Vendors = _context.VendorSignups.Count();
                ViewBag.Orders = _context.OrderTables.Count();
                ViewBag.Products = _context.OrderTables.Sum(o => (int?)o.TotalItems) ?? 0;
                
                // Total Revenue and Profit (Overall)
                decimal totalRevenue = _context.Payments.Sum(p => (decimal?)p.Amount) ?? 0m;
                ViewBag.TotalAmount = totalRevenue;
                ViewBag.Profit = Math.Round((double)(totalRevenue * 0.15m), 2); // 15% as requested

                // Weekly Analytics (Last 7 Days)
                decimal weeklyRevenue = _context.Payments
                    .Where(p => p.CreatedAt >= sevenDaysAgo)
                    .Sum(p => (decimal?)p.Amount) ?? 0m;
                ViewBag.WeeklyRevenue = weeklyRevenue;
                ViewBag.WeeklyProfit = Math.Round((double)(weeklyRevenue * 0.15m), 2);

                // Monthly Analytics (Current Month)
                decimal monthlyRevenue = _context.Payments
                    .Where(p => p.CreatedAt >= firstOfMonth)
                    .Sum(p => (decimal?)p.Amount) ?? 0m;
                ViewBag.MonthlyRevenue = monthlyRevenue;
                ViewBag.MonthlyProfit = Math.Round((double)(monthlyRevenue * 0.15m), 2);
            }
            catch
            {
                // Safe fallback to original raw-SQL approach if EF fails for any reason
                string cs = _configuration.GetConnectionString("DBCS")
                            ?? throw new Exception("DBCS not found");

                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                ViewBag.Users = GetValue(con, "SELECT COUNT(*) FROM UserSignup");
                ViewBag.Vendors = GetValue(con, "SELECT COUNT(*) FROM VendorSignup");
                ViewBag.Orders = GetValue(con, "SELECT COUNT(*) FROM OrderTable");
                ViewBag.Products = GetValue(con, "SELECT ISNULL(SUM(TotalItems),0) FROM OrderTable");
                
                string totalAmtStr = GetValue(con, "SELECT ISNULL(SUM(Amount),0) FROM Payment");
                decimal totalAmt = decimal.Parse(totalAmtStr);
                ViewBag.TotalAmount = totalAmt;
                ViewBag.Profit = totalAmt * 0.15m;

                // For fallback, we'll just set these to 0 or implement more SQL if needed, but EF should work.
                ViewBag.WeeklyRevenue = 0;
                ViewBag.WeeklyProfit = 0;
                ViewBag.MonthlyRevenue = 0;
                ViewBag.MonthlyProfit = 0;
            }
        }

        private string GetValue(SqlConnection con, string query)
        {
            object result = new SqlCommand(query, con).ExecuteScalar();
            return result?.ToString() ?? "0";
        }

        // Active orders JSON endpoint using EF Core
        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetActiveOrders()
        {
            try
            {
                var list = await _context.OrderTables
                    .Where(o => (o.Status ?? "").ToLower() != "cancelled")
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new
                    {
                        OrderId = o.OrderId,
                        CustomerName = o.CustomerName ?? "",
                        CustomerPhone = o.CustomerPhone ?? "",
                        TotalItems = o.TotalItems ?? 0,
                        PickupSlot = o.PickupSlot ?? "",
                        TotalCalories = o.TotalCalories ?? 0,
                        PaymentStatus = o.PaymentStatus ?? "",
                        Status = o.Status ?? "",
                        IsFlagged = o.IsFlagged ?? false,
                        CreatedAt = o.CreatedAt,
                        Amount = _context.Payments.Where(p => p.OrderId == o.OrderId).Select(p => p.Amount).FirstOrDefault() ?? 0m,
                        OrderType = o.OrderType ?? "Pickup",
                        DeliveryStatus = o.DeliveryStatus ?? "",
                        DeliveryAddress = o.DeliveryAddress ?? "",
                        DeliveryNotes = o.DeliveryNotes ?? ""
                    })
                    .ToListAsync();

                return Json(list);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            try
            {
                var o = await _context.OrderTables
                    .Include(x => x.OrderItems)
                    .ThenInclude(oi => oi.Food)
                    .Include(x => x.Payments)
                    .FirstOrDefaultAsync(x => x.OrderId == orderId);

                if (o == null) return Json(new { error = "Order not found" });

                var payment = o.Payments.FirstOrDefault();

                return Json(new
                {
                    OrderId = o.OrderId,
                    OrderDateTime = o.CreatedAt,
                    Status = o.Status ?? "",
                    CustomerName = o.CustomerName ?? "",
                    CustomerPhone = o.CustomerPhone ?? "",
                    PickupSlot = o.PickupSlot ?? "",
                    TotalCalories = o.TotalCalories ?? 0,
                    PaymentMode = payment?.PaymentMode ?? "",
                    Amount = payment?.Amount ?? 0m,
                    IsRefunded = payment?.IsRefunded ?? false,
                    RefundStatus = payment?.RefundStatus ?? "",
                    IsFlagged = o.IsFlagged ?? false,
                    OrderType = o.OrderType ?? "Pickup",
                    DeliveryAddress = o.DeliveryAddress ?? "",
                    DeliveryStatus = o.DeliveryStatus ?? "",
                    DeliveryPersonId = o.DeliveryPersonId,
                    DeliveryNotes = o.DeliveryNotes ?? "",
                    Items = o.OrderItems.Select(i => new
                    {
                        Name = i.ItemName ?? (i.Food != null ? i.Food.Name : ""),
                        Quantity = i.Quantity ?? 0,
                        Instructions = i.Instructions ?? "",
                        Price = i.Food != null ? i.Food.Price : 0m
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Accept / MarkReady / MarkPicked endpoints
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            var ok = await _orderService.UpdateOrderStatusAsync(orderId, "Accepted");
            if (!ok) return Json(new { success = false, message = "Order not found" });
            return Json(new { success = true, newStatus = "Accepted" });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReady(int orderId)
        {
            var ok = await _orderService.UpdateOrderStatusAsync(orderId, "Ready for Pickup");
            if (!ok) return Json(new { success = false, message = "Order not found" });
            return Json(new { success = true, newStatus = "Ready for Pickup" });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPicked(int orderId)
        {
            var ok = await _orderService.UpdateOrderStatusAsync(orderId, "Picked");
            if (!ok) return Json(new { success = false, message = "Order not found" });
            return Json(new { success = true, newStatus = "Picked" });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFlag(int orderId)
        {
            var ok = await _orderService.ToggleFlagAsync(orderId);
            if (!ok) return Json(new { success = false, message = "Order not found or update failed" });
            return Json(new { success = true });
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult FlaggedOrders()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetFlaggedOrders()
        {
            try
            {
                var list = await _context.OrderTables
                    .Where(o => o.IsFlagged == true)
                    .OrderByDescending(o => o.UpdatedAt)
                    .Select(o => new
                    {
                        OrderId = o.OrderId,
                        FlagReason = o.FlagReason ?? "Suspicious",
                        CustomerName = o.CustomerName ?? "",
                        CustomerPhone = o.CustomerPhone ?? "",
                        Amount = _context.Payments.Where(p => p.OrderId == o.OrderId).Select(p => p.Amount).FirstOrDefault() ?? 0m,
                        TotalCalories = o.TotalCalories ?? 0,
                        CreatedAt = o.CreatedAt,
                        IsResolved = o.IsResolved ?? false
                    })
                    .ToListAsync();

                return Json(list);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult PickupSlots()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetPickupSlots(DateTime? date)
        {
            try
            {
                var targetDate = date ?? DateTime.UtcNow.Date;
                var targetDateOnly = DateOnly.FromDateTime(targetDate);

                var slots = await _context.PickupSlots
                    .OrderBy(s => s.SlotLabel)
                    .Select(s => new
                    {
                        SlotId = s.SlotId,
                        SlotLabel = s.SlotLabel ?? "",
                        Capacity = s.Capacity ?? 0,
                        IsDisabled = s.IsDisabled ?? false,
                        // Count bookings for this slot on the target date
                        CurrentBookings = _context.OrderTables.Count(o => o.PickupSlot == s.SlotLabel && o.CreatedAt.HasValue && o.CreatedAt.Value.Date == targetDate.Date),
                        IsBlocked = _context.SlotBlocks.Any(b => b.SlotId == s.SlotId && b.BlockDate == targetDateOnly)
                    })
                    .ToListAsync();

                var list = slots.Select(s => new
                {
                    s.SlotId,
                    s.SlotLabel,
                    s.Capacity,
                    s.IsDisabled,
                    s.CurrentBookings,
                    s.IsBlocked,
                    Status = s.IsDisabled || s.IsBlocked ? "Blocked" : (s.CurrentBookings >= s.Capacity ? "Full" : "Available")
                }).ToList();

                return Json(list);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSlotStatus(int slotId, bool isDisabled)
        {
            var ok = await _orderService.UpdateSlotStatusAsync(slotId, isDisabled);
            return Json(new { success = ok, isDisabled });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSlotCapacity(int slotId, int capacity)
        {
            var res = await _orderService.UpdateSlotCapacityAsync(slotId, capacity);
            if (!res.ok) return Json(new { success = false, message = "Slot not found" });
            return Json(new { success = true, capacity = capacity, currentBookings = res.currentBookings });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSlotBlock(int slotId, DateTime date)
        {
            var res = await _orderService.ToggleSlotBlockAsync(slotId, date);
            return Json(new { success = res.ok, message = res.message });
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetFullSlotCount(DateTime? date)
        {
            var list = await _orderService.GetPickupSlotsAsync(date);
            var count = 0;
            foreach (var obj in list)
            {
                // object is anonymous -> use dynamic for convenience
                dynamic d = obj;
                if (d.Status == "Full") count++;
            }
            return Json(new { count });
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult CancelledOrders()
        {
            return View();
        }

        // Fixed GetCancelledOrders: EF-translatable filter, async, null-safe fields, error handling and route attribute.
        [AdminAuthorize]
        [HttpGet]
        [Route("Admin/GetCancelledOrders")]
        public async Task<IActionResult> GetCancelledOrders()
        {
            try
            {
                var list = await _context.OrderTables
                    // Use a simple-to-translate comparison to avoid EF translation errors
                    .Where(o => (o.Status ?? "").ToLower() == "cancelled")
                    .OrderByDescending(o => o.CancelledAt ?? o.CreatedAt)
                    .Select(o => new
                    {
                        OrderId = o.OrderId,
                        OrderCreatedAt = o.CreatedAt,
                        // Ensure CancelledAt is not null for the frontend (Step 4)
                        CancelledAt = o.CancelledAt ?? o.CreatedAt ?? System.DateTime.Now,
                        CancelReason = o.CancelReason ?? "",
                        CancelledBy = o.CancelledBy ?? "",
                        CustomerName = o.CustomerName ?? "",
                        // CustomerPhone may be null in schema; return empty string to be safe
                        CustomerPhone = o.CustomerPhone ?? "",
                        // Payment-related fields: use subqueries; they will return null if not present
                        Amount = _context.Payments.Where(p => p.OrderId == o.OrderId).Select(p => p.Amount).FirstOrDefault(),
                        RefundMethod = _context.Payments.Where(p => p.OrderId == o.OrderId).Select(p => p.RefundMethod).FirstOrDefault() ?? "",
                        RefundStatus = _context.Payments.Where(p => p.OrderId == o.OrderId).Select(p => p.RefundStatus).FirstOrDefault() ?? "",
                        AdminNotes = o.AdminNotes ?? ""
                    })
                    .ToListAsync();

                return Json(list);
            }
            catch (Exception ex)
            {
                // Return JSON error so frontend can display a message and we avoid a 500 HTML page
                return Json(new { error = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var ok = await _orderService.CancelOrderAsync(orderId);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerRefund(int orderId)
        {
            var ok = await _orderService.TriggerRefundAsync(orderId);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetOrderStatuses()
        {
            var statuses = new[]
            {
                new { Key = "New", Css = "status-new", Icon = "🟡", Tooltip = "New — awaiting acceptance by the kitchen" },
                new { Key = "Accepted", Css = "status-accepted", Icon = "🟠", Tooltip = "Accepted — vendor has accepted the order" },
                new { Key = "Ready for Pickup", Css = "status-ready", Icon = "🟢", Tooltip = "Ready — order is ready for customer pickup" },
                new { Key = "Picked", Css = "status-picked", Icon = "🔵", Tooltip = "Picked — order collected by customer" },
                new { Key = "Flagged", Css = "status-flagged", Icon = "⚠️", Tooltip = "Flagged — suspicious or requires attention" },
                new { Key = "Cancelled", Css = "status-cancelled", Icon = "⛔", Tooltip = "Cancelled — order was cancelled" }
            };

            return await Task.FromResult<IActionResult>(Json(statuses));
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetFlaggedCount()
        {
            var c = await _context.OrderTables.CountAsync(o => o.IsFlagged == true);
            return Json(new { count = c });
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetCancelledCount()
        {
            var c = await _context.OrderTables.CountAsync(o => (o.Status ?? "").ToLower() == "cancelled");
            return Json(new { count = c });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRefundStatus(int orderId, string status)
        {
            var ok = await _orderService.UpdateRefundStatusAsync(orderId, status);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAdminNote(int orderId, string note)
        {
            var ok = await _orderService.AddAdminNoteAsync(orderId, note);
            return Json(new { success = ok });
        }

        // Delivery Actions
        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetDeliveryPersonnel()
        {
            var list = await _orderService.GetAvailableDeliveryPersonnelAsync();
            return Json(list);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDelivery(int orderId, int deliveryPersonId)
        {
            var ok = await _orderService.AssignDeliveryPersonAsync(orderId, deliveryPersonId);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDeliveryStatus(int orderId, string status)
        {
            var ok = await _orderService.UpdateDeliveryStatusAsync(orderId, status);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockCustomer(string customerPhone)
        {
            var ok = await _orderService.BlockCustomerAsync(customerPhone);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult OrderManagement()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> DeliveryDashboard()
        {
            try
            {
                // Active deliveries (Assigned or Out for Delivery)
                var activeDeliveries = await _context.OrderTables
                    .Where(o => o.OrderType == "Delivery" && 
                                (o.DeliveryStatus == "Assigned" || o.DeliveryStatus == "Out for Delivery" || o.Status == "Ready for Pickup"))
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new
                    {
                        o.OrderId,
                        o.CustomerName,
                        o.CustomerPhone,
                        o.DeliveryAddress,
                        o.DeliveryStatus,
                        o.Status,
                        o.CreatedAt,
                        o.DeliveryNotes,
                        o.DeliveryPersonId
                    })
                    .ToListAsync();

                // Stats
                ViewBag.TotalDeliveries = await _context.OrderTables.CountAsync(o => o.OrderType == "Delivery");
                ViewBag.PendingDeliveries = await _context.OrderTables.CountAsync(o => o.OrderType == "Delivery" && (o.DeliveryStatus == "Assigned" || o.DeliveryStatus == "Out for Delivery"));
                ViewBag.CompletedDeliveries = await _context.OrderTables.CountAsync(o => o.OrderType == "Delivery" && o.DeliveryStatus == "Delivered");
                ViewBag.CancelledDeliveries = await _context.OrderTables.CountAsync(o => o.OrderType == "Delivery" && o.Status == "Cancelled");

                return View(activeDeliveries);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error loading delivery dashboard: " + ex.Message;
                return View(new List<object>());
            }
        }

        // Render Order Details view (page will request JSON /GetOrderDetails)
        [AdminAuthorize]
        [HttpGet]
        public IActionResult OrderDetails(int? id = null)
        {
            ViewBag.OrderId = id;
            return View();
        }

        // Approve / Reject vendor actions
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveVendor(int id)
        {
            if (id <= 0) return BadRequest();

            var vendor = _context.VendorSignups.FirstOrDefault(v => v.VendorId == id);
            if (vendor == null) return NotFound();

            vendor.IsApproved = true;
            vendor.IsRejected = false;
            // optionally set approval timestamp (if you have a column)
            _context.SaveChanges();

            TempData["Success"] = "Vendor approved successfully.";
            return RedirectToAction("NewVendorRequest");
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectVendor(int id)
        {
            if (id <= 0) return BadRequest();

            var vendor = _context.VendorSignups.FirstOrDefault(v => v.VendorId == id);
            if (vendor == null) return NotFound();

            vendor.IsRejected = true;
            vendor.IsApproved = false;
            _context.SaveChanges();

            TempData["Success"] = "Vendor rejected.";
            return RedirectToAction("NewVendorRequest");
        }
    }
}

































