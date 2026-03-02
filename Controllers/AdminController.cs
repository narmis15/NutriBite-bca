using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using NutriBite.Filters;
using NUTRIBITE.Models;
using NUTRIBITE.Services;
using System.Threading.Tasks;

namespace NUTRIBITE.Controllers
{
    public partial class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IOrderService _orderService;

        public AdminController(IConfiguration configuration, IOrderService orderService)
        {
            _configuration = configuration;
            _orderService = orderService;
        }

        // 🔓 PUBLIC
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 🔓 PUBLIC
        [HttpPost]
        public IActionResult Login(string email, string Password)
        {
            if (email== "Nutribite123@gmail.com" &&
                Password == "NutriBite//26")
            {
                HttpContext.Session.SetString("Admin", email);
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
            List<Vendor> vendors = new List<Vendor>();

            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DBCS")))
            {
                string query = "SELECT * FROM VendorSignup";

                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    vendors.Add(new Vendor
                    {
                        VendorId = Convert.ToInt32(reader["VendorId"]),
                        VendorName = reader["VendorName"].ToString(),
                        Email = reader["Email"].ToString(),
                        IsApproved = Convert.ToBoolean(reader["IsApproved"]),
                        IsRejected = Convert.ToBoolean(reader["IsRejected"]),
                        CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                    });
                }
            }

            return View(vendors);
        }
        [AdminAuthorize]
        [HttpGet]
        public IActionResult NewVendorRequest()
        {
            List<object> vendors = new List<object>();

            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                string query = @"SELECT VendorId, VendorName, Email, CreatedAt 
                         FROM VendorSignup 
                         WHERE IsApproved = 0 AND IsRejected = 0";

                SqlCommand cmd = new SqlCommand(query, con);
                SqlDataReader dr = cmd.ExecuteReader();

                while (dr.Read())
                {
                    vendors.Add(new
                    {
                        VendorId = Convert.ToInt32(dr["VendorId"]),
                        VendorName = dr["VendorName"].ToString(),
                        Email = dr["Email"].ToString(),
                        CreatedAt = Convert.ToDateTime(dr["CreatedAt"])
                    });
                }
            }

            return View(vendors);
        }

        // 🔒 Admin only
        [AdminAuthorize]
        [HttpGet]
        public IActionResult AddFoodCategory()
        {
            return View();
        }

        // 🔒 Admin only
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
            string imagePath = "";
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

            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");
            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            string query = @"
        INSERT INTO FoodCategory
        (ProductCategory, MealCategory, ImagePath)
        VALUES (@pc, @mc, @img)";

            SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@pc", finalProductCategory);
            cmd.Parameters.AddWithValue("@mc", finalMealCategory);
            cmd.Parameters.AddWithValue("@img", imagePath);

            con.Open();
            cmd.ExecuteNonQuery();

            ViewBag.Success = "Food category added successfully";
            return View();
        }

        // 🔒 Admin only - Add Coupon (GET)
        [AdminAuthorize]
        [HttpGet]
        public IActionResult AddCoupon()
        {
            return View();
        }

        // 🔒 Admin only - Add Coupon (POST)
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
                string cs = _configuration.GetConnectionString("DBCS")
                            ?? throw new Exception("DBCS not found");

                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Optional: prevent duplicate coupon codes
                string existsQuery = "SELECT COUNT(1) FROM Coupons WHERE CouponCode = @code";
                using (SqlCommand exCmd = new SqlCommand(existsQuery, con))
                {
                    exCmd.Parameters.AddWithValue("@code", CouponCode);
                    int exists = Convert.ToInt32(exCmd.ExecuteScalar() ?? 0);
                    if (exists > 0)
                    {
                        ModelState.AddModelError(nameof(CouponCode), "A coupon with this code already exists.");
                        return View();
                    }
                }

                string insertQuery = @"
                    INSERT INTO Coupons (CouponCode, Discount, StartDate, EndDate, CreatedAt)
                    VALUES (@code, @discount, @start, @end, @created)";

                using SqlCommand cmd = new SqlCommand(insertQuery, con);
                cmd.Parameters.AddWithValue("@code", CouponCode);
                cmd.Parameters.AddWithValue("@discount", Discount);
                cmd.Parameters.AddWithValue("@start", StartDate);
                cmd.Parameters.AddWithValue("@end", EndDate);
                cmd.Parameters.AddWithValue("@created", DateTime.Now);

                cmd.ExecuteNonQuery();

                ViewBag.Success = "Coupon added successfully";
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred while adding the coupon: " + ex.Message;
            }

            return View();
        }

        public IActionResult ViewCategory(string sortOrder)
        {
            ViewBag.IdSort = sortOrder == "id" ? "id_desc" : "id";
            ViewBag.CategorySort = sortOrder == "cat" ? "cat_desc" : "cat";

            List<FoodCategory> list = new List<FoodCategory>();

            string query = "SELECT cid, ProductCategory, ProductPic, MealCategory, ImagePath, CreatedAt FROM AddCategory WHERE ProductCategory IS NOT NULL\r\n  AND ProductCategory <> 'NA'";

            if (sortOrder == "id")
                query += " ORDER BY cid";
            else if (sortOrder == "id_desc")
                query += " ORDER BY cid DESC";
            else if (sortOrder == "cat")
                query += " ORDER BY ProductCategory";
            else if (sortOrder == "cat_desc")
                query += " ORDER BY ProductCategory DESC";

            using (SqlConnection con =
                new SqlConnection(_configuration.GetConnectionString("DBCS")))
            {
                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();

                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new FoodCategory
                    {
                        cid = Convert.ToInt32(dr["cid"]),
                        ProductCategory = dr["ProductCategory"].ToString(),
                        ProductPic = dr["ProductPic"].ToString(),
                        MealCategory = dr["MealCategory"].ToString(),
                        ImagePath = dr["ImagePath"].ToString(),
                        CreatedAt = Convert.ToDateTime(dr["CreatedAt"])
                    });
                }
            }

            return View(list);
        }
        public IActionResult ViewMealCategory(string sortOrder)
        {
            ViewBag.IdSort = sortOrder == "id" ? "id_desc" : "id";
            ViewBag.MealSort = sortOrder == "meal" ? "meal_desc" : "meal";

            List<FoodCategory> list = new List<FoodCategory>();

            string query = @"SELECT cid, MealCategory, MealPic 
                     FROM dbo.AddCategory 
                     WHERE MealCategory IS NOT NULL";

            if (sortOrder == "id")
                query += " ORDER BY cid";
            else if (sortOrder == "id_desc")
                query += " ORDER BY cid DESC";
            else if (sortOrder == "meal")
                query += " ORDER BY MealCategory";
            else if (sortOrder == "meal_desc")
                query += " ORDER BY MealCategory DESC";

            using (SqlConnection con =
                new SqlConnection(_configuration.GetConnectionString("DBCS")))
            {
                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();

                SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new FoodCategory
                    {
                        cid = Convert.ToInt32(dr["cid"]),
                        MealCategory = dr["MealCategory"].ToString(),
                        MealPic = dr["MealPic"] == DBNull.Value
                                     ? "default.jpg"
                                     : dr["MealPic"].ToString()
                    });
                }
            }

            return View(list);
        }



        // 🔓 PUBLIC
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private void LoadDashboardCounts()
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            ViewBag.Users = GetValue(con, "SELECT COUNT(*) FROM UserSignup");
            ViewBag.Vendors = GetValue(con, "SELECT COUNT(*) FROM VendorSignup");
            ViewBag.Orders = GetValue(con, "SELECT COUNT(*) FROM OrderTable");
            ViewBag.Products = GetValue(con, "SELECT ISNULL(SUM(TotalItems),0) FROM OrderTable");
            ViewBag.TotalAmount = GetValue(con, "SELECT ISNULL(SUM(Amount),0) FROM Payment");
            ViewBag.Profit = GetValue(con, "SELECT ISNULL(SUM(Amount),0) * 0.10 FROM Payment");
        }

        private string GetValue(SqlConnection con, string query)
        {
            object result = new SqlCommand(query, con).ExecuteScalar();
            return result?.ToString() ?? "0";
        }

        // Active orders JSON endpoint
        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetActiveOrders()
        {
            var list = await _orderService.GetActiveOrdersAsync();
            return Json(list);
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var details = await _orderService.GetOrderDetailsAsync(orderId);
            if (details == null) return Json(new { error = "Order not found" });
            return Json(details);
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
        public async Task<IActionResult> FlaggedOrders()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetFlaggedOrders()
        {
            var list = await _orderService.GetFlaggedOrdersAsync();
            return Json(list);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyFlag(int orderId)
        {
            var ok = await _orderService.VerifyFlagAsync(orderId);
            return Json(new { success = ok });
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> PickupSlots()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetPickupSlots(DateTime? date)
        {
            var list = await _orderService.GetPickupSlotsAsync(date);
            return Json(list);
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
        public async Task<IActionResult> CancelledOrders()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetCancelledOrders()
        {
            var list = await _orderService.GetCancelledOrdersAsync();
            return Json(list);
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

            return Json(statuses);
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetFlaggedCount()
        {
            var c = await _orderService.GetFlaggedCountAsync();
            return Json(new { count = c });
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> GetCancelledCount()
        {
            var c = await _orderService.GetCancelledCountAsync();
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

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockCustomer(string customerPhone)
        {
            var ok = await _orderService.BlockCustomerAsync(customerPhone);
            return Json(new { success = ok });
        }

        // Render Order Management view (uses existing AJAX JSON endpoints)
        [AdminAuthorize]
        [HttpGet]
        public IActionResult OrderManagement()
        {
            return View();
        }

        // Render Order Details view (page will request JSON /GetOrderDetails)
        [AdminAuthorize]
        [HttpGet]
        public IActionResult OrderDetails(int? id = null)
        {
            ViewBag.OrderId = id;
            return View();
        }
    }
}



