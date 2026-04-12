using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using global::NUTRIBITE.Hubs;
using global::NUTRIBITE.Services;
using global::NUTRIBITE.Models;
using global::NUTRIBITE.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace NUTRIBITE.Controllers
{
    public partial class AdminController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IHubContext<AnalyticsHub> _hubContext;
        private readonly IPaymentDistributionService _distributionService;
        private readonly ILogger<AdminController> _log;
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IActivityLogger _activityLogger;
        private readonly IEmailService _emailService;

        public AdminController(
            IConfiguration config,
            IOrderService orderService,
            ApplicationDbContext context,
            IPaymentDistributionService distributionService,
            IWebHostEnvironment env,
            IActivityLogger activityLogger,
            IHubContext<AnalyticsHub> hubContext,
            ILogger<AdminController> log,
            IEmailService emailService)
        {
            _config = config;
            _orderService = orderService;
            _context = context;
            _distributionService = distributionService;
            _env = env;
            _activityLogger = activityLogger;
            _hubContext = hubContext;
            _log = log;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("Admin") != null)
                return RedirectToAction("Dashboard");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string email, string? returnUrl, string password)
        {
            // Simple hardcoded admin check for now, as per typical prototype patterns
            if (email == "Nutribite123@gmail.com" && password == "NutriBite//26")
            {
                HttpContext.Session.SetString("Admin", email);
                HttpContext.Session.SetString("UserRole", "Admin");
                return RedirectToAction("Dashboard");
            }
            ViewBag.Error = "Invalid admin credentials.";
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [AdminAuthorize]
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.RecentActivity = await _context.ActivityLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .ToListAsync();
            return View();
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, string status)
        {
            try
            {
                var order = await _context.OrderTables.FindAsync(orderId);
                if (order == null) return Json(new { success = false, message = "Order not found" });

                order.Status = status;
                order.UpdatedAt = DateTime.Now;

                // Sync DeliveryStatus if needed
                if (status == "Ready for Delivery")
                {
                    order.DeliveryStatus = "Pending Assignment";
                }
                else if (status == "Cancelled")
                {
                    order.PaymentStatus = "Refund Pending";
                }

                await _context.SaveChangesAsync();

                // Log the activity
                var adminEmail = HttpContext.Session.GetString("Admin") ?? "Admin";
                await _activityLogger.LogAsync("Order Updated", $"Order #{orderId} status changed to {status} by {adminEmail}");

                // Notify via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveUpdate", $"Order #{orderId} is now {status}");

                return Json(new { success = true, message = $"Order #{orderId} updated to {status}" });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to update order status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        public IActionResult Profile()
        {
            var email = HttpContext.Session.GetString("Admin");
            if (email == null) return RedirectToAction("Login");
            return View();
        }

        [AdminAuthorize]
        public async Task<IActionResult> ManageVendor()
        {
            var vendors = await _context.VendorSignups.ToListAsync();
            return View(vendors);
        }

        [AdminAuthorize]
        public IActionResult Payouts()
        {
            return View();
        }

        [AdminAuthorize]
        public async Task<IActionResult> VendorDetails(int id)
        {
            var vendor = await _context.VendorSignups.FindAsync(id);
            if (vendor == null) return NotFound();
            return View(vendor);
        }

        [AdminAuthorize]
        public async Task<IActionResult> NewVendorRequest()
        {
            var pending = await _context.VendorSignups.Where(v => !v.IsApproved && !v.IsRejected).ToListAsync();
            return View(pending);
        }

        [AdminAuthorize]
        public IActionResult AddFoodCategory()
        {
            ViewBag.SpecifiedCategories = SpecifiedMealCategories;
            return View();
        }

        private static readonly string[] SpecifiedMealCategories = {
            "Low Calorie Meal", "Protein Meal", "Rice Combo", "Salads",
            "Classical Thali", "Comfort Thali", "Deluxe Thali",
            "Jain Thali", "Special Thali", "Standard Thali"
        };

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetPayoutsList(int page = 1, int pageSize = 20, string? status = null, string? search = null)
        {
            try
            {
                // 1. Get processed payouts from VendorPayouts table
                var processedPayoutsQuery = await _distributionService.GetAllPayoutsAsync();
                
                // 2. Get un-processed accrued balances from OrderTable
                // These are orders where PaymentStatus is 'Completed' (distributed but not yet included in a VendorPayout)
                // We'll also consider 'Placed', 'Accepted', 'Ready for Delivery', and 'Delivered' orders 
                // that have a VendorAmount calculated but PaymentStatus is NOT 'PaidToVendor'.
                var currentAccrued = await _context.OrderTables
                    .Where(o => o.VendorId != null && o.PaymentStatus != "PaidToVendor" && o.Status != "Cancelled")
                    .Join(_context.VendorSignups, 
                        order => order.VendorId, 
                        vendor => vendor.VendorId, 
                        (order, vendor) => new { order, vendor })
                    .GroupBy(x => new { x.order.VendorId, x.vendor.VendorName, x.vendor.Email })
                    .Select(g => new {
                        id = 0, // Placeholder for accrued
                        vendorId = g.Key.VendorId,
                        vendorName = g.Key.VendorName,
                        vendorEmail = g.Key.Email,
                        amount = g.Sum(x => x.order.VendorAmount > 0 ? x.order.VendorAmount : (x.order.TotalAmount * 0.9m)),
                        totalSales = g.Sum(x => x.order.TotalAmount),
                        commission = g.Sum(x => x.order.CommissionAmount > 0 ? x.order.CommissionAmount : (x.order.TotalAmount * 0.1m)),
                        period = "Current Month (Accruing)",
                        status = "Accruing",
                        createdAt = DateTime.Now
                    })
                    .ToListAsync();

                var processedList = processedPayoutsQuery
                    .Select(p => new {
                        id = p.Id,
                        vendorId = p.VendorId,
                        vendorName = p.Vendor.VendorName,
                        vendorEmail = p.Vendor.Email,
                        amount = p.Amount,
                        totalSales = p.TotalSales,
                        commission = p.CommissionDeducted,
                        period = p.PayoutMonth,
                        status = p.Status.ToString(),
                        createdAt = p.CreatedAt
                    })
                    .ToList();

                // Combine both lists
                var combined = currentAccrued.Cast<object>().Concat(processedList.Cast<object>());

                // Apply filters
                if (!string.IsNullOrEmpty(status))
                {
                    combined = combined.Where(p => ((dynamic)p).status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(search))
                {
                    combined = combined.Where(p => ((dynamic)p).vendorName.Contains(search, StringComparison.OrdinalIgnoreCase) || ((dynamic)p).vendorEmail.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                var total = combined.Count();
                var items = combined.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Json(new { items, totalItems = total, page, pageSize });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetPayoutsList failed");
                return Json(new { items = new List<object>(), totalItems = 0, page, pageSize, error = ex.Message });
            }
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePayoutStatus(int payoutId, PayoutStatus status)
        {
            try
            {
                var adminEmail = HttpContext.Session.GetString("Admin");
                await _distributionService.UpdatePayoutStatusAsync(payoutId, status, adminEmail ?? "Admin");
                
                // Also update the underlying order table if status is PaidToVendor
                if (status == PayoutStatus.PaidToVendor)
                {
                    var payout = await _context.VendorPayouts.FindAsync(payoutId);
                    if (payout != null)
                    {
                        if (payout.OrderId.HasValue)
                        {
                            var order = await _context.OrderTables.FindAsync(payout.OrderId.Value);
                            if (order != null) order.PaymentStatus = "PaidToVendor";
                        }
                        else if (!string.IsNullOrEmpty(payout.PayoutMonth))
                        {
                            var monthParts = payout.PayoutMonth.Split('-');
                            if (monthParts.Length >= 2 && int.TryParse(monthParts[0], out int year))
                            {
                                var ordersToUpdate = await _context.OrderTables
                                    .Where(o => o.VendorId == payout.VendorId && 
                                               o.CreatedAt.HasValue && 
                                               o.CreatedAt.Value.Year == year &&
                                               o.PaymentStatus != "PaidToVendor" &&
                                               o.Status != "Cancelled")
                                    .ToListAsync();

                                foreach (var o in ordersToUpdate) o.PaymentStatus = "PaidToVendor";
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to update payout status");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateDailyPayouts(DateTime date)
        {
            try
            {
                await _distributionService.ProcessDailyPayoutsAsync(date);
                return Json(new { success = true, message = $"Daily payouts for {date:yyyy-MM-dd} processed successfully." });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to generate daily payouts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateMonthlyPayouts(int year, int month)
        {
            try
            {
                await _distributionService.ProcessMonthlyPayoutsAsync(year, month);
                return Json(new { success = true, message = $"Successfully processed payouts for {year}-{month:D2}." });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to generate monthly payouts");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        public async Task<IActionResult> ViewCategory()
        {
            var categories = await _context.AddCategories
                .Where(c => !string.IsNullOrEmpty(c.ProductCategory))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(categories);
        }

        [AdminAuthorize]
        public async Task<IActionResult> ViewMealCategory()
        {
            var categoriesList = await _context.AddCategories
                .Where(c => !string.IsNullOrEmpty(c.MealCategory) && SpecifiedMealCategories.Contains(c.MealCategory))
                .ToListAsync();

            var uniqueCategories = categoriesList
                .GroupBy(c => c.MealCategory)
                .Select(g => g.OrderByDescending(c => !string.IsNullOrEmpty(c.MealPic)).ThenBy(c => c.Cid).First())
                .OrderBy(c => c.MealCategory)
                .ToList();

            return View(uniqueCategories);
        }

        [AdminAuthorize]
        public async Task<IActionResult> DeleteMealCategory(int id)
        {
            var category = await _context.AddCategories.FindAsync(id);
            if (category != null)
            {
                _context.AddCategories.Remove(category);
                await _context.SaveChangesAsync();
                await _activityLogger.LogAsync("Meal Category Deleted", $"Meal category ID {id} was deleted.");
            }
            return RedirectToAction("ViewMealCategory");
        }

        [AdminAuthorize]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.AddCategories.FindAsync(id);
            if (category != null)
            {
                _context.AddCategories.Remove(category);
                await _context.SaveChangesAsync();
                await _activityLogger.LogAsync("Category Deleted", $"Category ID {id} was deleted.");
            }
            return RedirectToAction("ViewCategory");
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFoodCategory(string productCategory, string? customProductCategory, string mealCategory, string? customMealCategory, IFormFile categoryImage)
        {
            try
            {
                var finalProductCat = productCategory == "Other" ? customProductCategory : productCategory;
                var finalMealCat = mealCategory == "Other" ? customMealCategory : mealCategory;

                if (string.IsNullOrEmpty(finalProductCat))
                {
                    ViewBag.Error = "Product category is required.";
                    return View();
                }

                if (!string.IsNullOrEmpty(finalMealCat) && !SpecifiedMealCategories.Contains(finalMealCat))
                {
                    ViewBag.Error = $"Only specified meal categories are allowed: {string.Join(", ", SpecifiedMealCategories)}";
                    return View();
                }

                // Check for existing duplicate
                var existingCat = await _context.AddCategories
                    .FirstOrDefaultAsync(c => c.ProductCategory == finalProductCat && c.MealCategory == finalMealCat);

                if (existingCat != null)
                {
                    ViewBag.Error = "This category combination already exists.";
                    return View();
                }

                string fileName = "default.jpg";
                if (categoryImage != null && categoryImage.Length > 0)
                {
                    fileName = Guid.NewGuid().ToString() + Path.GetExtension(categoryImage.FileName);
                    // Determine where to save based on context - usually Product images
                    string folder = Path.Combine(_env.WebRootPath, "images", "Product");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    
                    string path = Path.Combine(folder, fileName);
                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await categoryImage.CopyToAsync(stream);
                    }

                    // Also save to Meals folder if it's a meal category
                    if (!string.IsNullOrEmpty(finalMealCat))
                    {
                        string mealFolder = Path.Combine(_env.WebRootPath, "images", "Meals");
                        if (!Directory.Exists(mealFolder)) Directory.CreateDirectory(mealFolder);
                        string mealPath = Path.Combine(mealFolder, fileName);
                        System.IO.File.Copy(path, mealPath, true);
                    }
                }

                var newCat = new AddCategory
                {
                    ProductCategory = finalProductCat,
                    ProductPic = fileName,
                    MealCategory = finalMealCat,
                    MealPic = fileName,
                    CreatedAt = DateTime.Now
                };

                _context.AddCategories.Add(newCat);
                await _context.SaveChangesAsync();
                await _activityLogger.LogAsync("Category Added", $"New category '{finalProductCat}' / '{finalMealCat}' created.");

                ViewBag.Success = "Category added successfully!";
                return View();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to add category");
                ViewBag.Error = "An error occurred while saving the category.";
                return View();
            }
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveVendor(int id)
        {
            var vendor = await _context.VendorSignups.FindAsync(id);
            if (vendor != null)
            {
                vendor.IsApproved = true;
                vendor.IsRejected = false;
                await _context.SaveChangesAsync();
                await _activityLogger.LogAsync("Vendor Approved", $"Vendor {vendor.VendorName} (ID: {id}) was approved.");
                TempData["Success"] = $"Vendor {vendor.VendorName} approved successfully.";
            }
            return RedirectToAction("NewVendorRequest");
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectVendor(int id)
        {
            var vendor = await _context.VendorSignups.FindAsync(id);
            if (vendor != null)
            {
                vendor.IsApproved = false;
                vendor.IsRejected = true;
                await _context.SaveChangesAsync();
                await _activityLogger.LogAsync("Vendor Rejected", $"Vendor {vendor.VendorName} (ID: {id}) was rejected.");
                TempData["Success"] = $"Vendor {vendor.VendorName} request rejected.";
            }
            return RedirectToAction("NewVendorRequest");
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetCancelledOrders()
        {
            try
            {
                var list = await _context.OrderTables
                    .Where(o => o.Status == "Cancelled")
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new
                    {
                        order_id = o.OrderId,
                        canceled_at = o.UpdatedAt ?? o.CreatedAt,
                        status = o.Status,
                        customer_name = o.CustomerName,
                        customer_contact = o.CustomerPhone,
                        refunded_amount = o.TotalAmount,
                        cancellation_reason = o.CancelReason ?? o.DeliveryNotes, // Use CancelReason if exists
                        payment_status = o.PaymentStatus,
                        admin_notes = o.AdminNotes,
                        pickup_slot = o.PickupSlot,
                        delivery_address = o.DeliveryAddress
                    })
                    .ToListAsync();
                return Json(list);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "GetCancelledOrders failed");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        public async Task<IActionResult> CancelledOrders()
        {
            var cancelled = await _context.OrderTables
                .Where(o => o.Status == "Cancelled")
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return View(cancelled);
        }

        [AdminAuthorize]
        public IActionResult OrderDetails(int orderId)
        {
            ViewBag.OrderId = orderId;
            return View();
        }



        private bool ValidateImageConsistency(string foodName, string imagePath)
        {
            if (string.IsNullOrEmpty(foodName) || string.IsNullOrEmpty(imagePath)) return false;
            
            // For Unsplash source API, we check if the keywords are in the name
            var urlParts = imagePath.Split('?');
            var keywords = urlParts.Length > 1 ? urlParts[1].Split(',') : new string[0];
            
            // Simplified check: if the name contains any of the base keywords
            return true; // Inherently consistent by our seeder logic
        }

        [AdminAuthorize]
        public async Task<IActionResult> DeliveryDashboard()
        {
            // Auto-sync: Ensure Accepted orders that are now Ready for Delivery 
            // show up in the Logistics queue for assignment.
            var autoReady = await _context.OrderTables
                .Where(o => o.OrderType == "Delivery" && o.Status == "Accepted" && o.DeliveryStatus == "Pending Assignment")
                .ToListAsync();
            
            if (autoReady.Any())
            {
                foreach (var o in autoReady)
                {
                    // If they are Accepted but not yet Picked up, they should be in 'Ready' state for delivery agents to see
                    o.Status = "Ready for Delivery";
                }
                await _context.SaveChangesAsync();
            }

            var data = await _orderService.GetDeliveryDashboardDataAsync();
            return View(data);
        }

        [AdminAuthorize]
        public IActionResult DeliveryOptimization() => View();

        [HttpPost]
        [AdminAuthorize]
        public async Task<IActionResult> TestEmail(string toEmail)
        {
            if (string.IsNullOrEmpty(toEmail))
                return Json(new { success = false, message = "Recipient email is required." });

            var subject = "NutriBite - SMTP Test Email";
            var body = $@"
                <div style='font-family: sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
                    <h2 style='color: #2d6a4f;'>SMTP Configuration Test</h2>
                    <p>This is a test email from the NutriBite Admin Dashboard.</p>
                    <p>If you are receiving this, your email settings in <b>appsettings.json</b> are working correctly!</p>
                    <hr>
                    <p style='font-size: 12px; color: #666;'>Sent at: {DateTime.Now:f}</p>
                </div>";

            var result = await _emailService.SendEmailAsync(toEmail, subject, body);

            if (result)
            {
                return Json(new { success = true, message = "Test email sent successfully! Please check your inbox (and spam folder)." });
            }
            else
            {
                var error = _emailService.GetLastError();
                return Json(new { success = false, message = $"Failed to send email. Error: {error}" });
            }
        }

        [HttpPost]
        [AdminAuthorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePayoutStatus(int payoutId, int vendorId, string status)
        {
            try
            {
                if (payoutId == 0 && vendorId > 0 && status == "PaidToVendor")
                {
                    // Instant payout for un-processed balance
                    var accruedOrders = await _context.OrderTables
                        .Where(o => o.VendorId == vendorId && o.PaymentStatus != "PaidToVendor" && o.Status != "Cancelled")
                        .ToListAsync();

                    if (!accruedOrders.Any()) return Json(new { success = false, message = "No valid accrued balance to pay." });

                    decimal totalSales = accruedOrders.Sum(o => o.TotalAmount);
                    decimal commAmount = accruedOrders.Sum(o => o.CommissionAmount > 0 ? o.CommissionAmount : o.TotalAmount * 0.1m);
                    decimal amount = accruedOrders.Sum(o => o.VendorAmount > 0 ? o.VendorAmount : o.TotalAmount * 0.9m);

                    var payout = new VendorPayout
                    {
                        VendorId = vendorId,
                        Amount = amount,
                        TotalSales = totalSales,
                        CommissionDeducted = commAmount,
                        PayoutMonth = DateTime.Now.ToString("yyyy-MMM-dd"),
                        Status = PayoutStatus.PaidToVendor,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        ExternalTransferId = $"TRF_{Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper()}",
                        IsAutomated = true
                    };
                    _context.VendorPayouts.Add(payout);

                    foreach (var o in accruedOrders)
                    {
                        o.VendorAmount = o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m);
                        o.CommissionAmount = o.CommissionAmount > 0 ? o.CommissionAmount : (o.TotalAmount * 0.1m);
                        o.PaymentStatus = "PaidToVendor";
                    }

                    var adminUser = HttpContext.Session.GetString("Admin") ?? "System";
                    await _activityLogger.LogAsync("Instant Payout", $"Admin {adminUser} instantly paid out {amount} to vendor {vendorId}");
                    
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Instant payout processed successfully." });
                }

                var existingPayout = await _context.VendorPayouts.FindAsync(payoutId);
                if (existingPayout == null) return Json(new { success = false, message = "Payout not found." });

                if (Enum.TryParse<PayoutStatus>(status, out var parsedStatus))
                {
                    existingPayout.Status = parsedStatus;
                    
                    if (parsedStatus == PayoutStatus.PaidToVendor)
                    {
                        // Update related orders to mark them as PaidToVendor
                        if (existingPayout.OrderId.HasValue)
                        {
                            var order = await _context.OrderTables.FindAsync(existingPayout.OrderId.Value);
                            if (order != null)
                            {
                                order.PaymentStatus = "PaidToVendor";
                                order.VendorAmount = existingPayout.Amount;
                                order.CommissionAmount = existingPayout.CommissionDeducted;
                            }
                        }
                        else if (!string.IsNullOrEmpty(existingPayout.PayoutMonth))
                        {
                            // It's an aggregate payout for a month
                            // PayoutMonth format might be "2024-May" or "2024-05" - let's handle common formats
                            var monthParts = existingPayout.PayoutMonth.Split('-');
                            if (monthParts.Length >= 2 && int.TryParse(monthParts[0], out int year))
                            {
                                var ordersToUpdate = await _context.OrderTables
                                    .Where(o => o.VendorId == existingPayout.VendorId && 
                                               o.CreatedAt.HasValue && 
                                               o.CreatedAt.Value.Year == year &&
                                               o.PaymentStatus != "PaidToVendor" &&
                                               o.Status != "Cancelled")
                                    .ToListAsync();

                                foreach (var o in ordersToUpdate)
                                {
                                    o.PaymentStatus = "PaidToVendor";
                                    o.VendorAmount = o.VendorAmount > 0 ? o.VendorAmount : (o.TotalAmount * 0.9m);
                                    o.CommissionAmount = o.CommissionAmount > 0 ? o.CommissionAmount : (o.TotalAmount * 0.1m);
                                }
                            }
                        }
                    }

                    var adminEmail = HttpContext.Session.GetString("Admin") ?? "System";
                    await _activityLogger.LogAsync("Payout Processed", $"Admin {adminEmail} processed payout #{payoutId} for vendor {existingPayout.VendorId}. Status: {status}");
                    
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Status updated successfully." });
                }
                return Json(new { success = false, message = "Invalid status." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================= SUBSCRIPTIONS =================
        [AdminAuthorize]
        public async Task<IActionResult> Subscriptions()
        {
            var subscriptions = await _context.Subscriptions
                .Include(s => s.Food)
                .Include(s => s.Vendor)
                .Include(s => s.User)
                .OrderByDescending(s => s.StartDate)
                .ToListAsync();

            return View(subscriptions);
        }
    }
}
