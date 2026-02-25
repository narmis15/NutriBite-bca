using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using NutriBite.Filters;
using NUTRIBITE.Models;


namespace NUTRIBITE.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;

        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 🔓 PUBLIC
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 🔓 PUBLIC
        [HttpPost]
        public IActionResult Login(string UserId, string Password)
        {
            if (UserId == "Nutribite123@gmail.com" &&
                Password == "NutriBite//26")
            {
                HttpContext.Session.SetString("Admin", UserId);
                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid UserId or Password";
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
            ViewBag.Products = GetValue(con, "SELECT ISNULL(SUM(Qty),0) FROM OrderTable");
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
        public IActionResult GetActiveOrders()
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            string query = @"
        SELECT OrderId, CustomerName, CustomerPhone, TotalItems, PickupSlot, TotalCalories, PaymentStatus, Status, IsFlagged, CreatedAt
        FROM OrderTable
        WHERE Status <> 'Cancelled'"; // adjust if you want more filtering

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                using SqlCommand cmd = new SqlCommand(query, con);
                con.Open();
                using var dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        OrderId = Convert.ToInt32(dr["OrderId"]),
                        CustomerName = dr["CustomerName"]?.ToString() ?? "",
                        CustomerPhone = dr["CustomerPhone"]?.ToString() ?? "",
                        TotalItems = dr["TotalItems"] == DBNull.Value ? 0 : Convert.ToInt32(dr["TotalItems"]),
                        PickupSlot = dr["PickupSlot"]?.ToString() ?? "",
                        TotalCalories = dr["TotalCalories"] == DBNull.Value ? 0 : Convert.ToInt32(dr["TotalCalories"]),
                        PaymentStatus = dr["PaymentStatus"]?.ToString() ?? "",
                        Status = dr["Status"]?.ToString() ?? "",
                        IsFlagged = dr["IsFlagged"] != DBNull.Value && Convert.ToInt32(dr["IsFlagged"]) == 1,
                        CreatedAt = dr["CreatedAt"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(dr["CreatedAt"])
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }

            return Json(list);
        }

        // Accept / MarkReady / MarkPicked endpoints
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AcceptOrder(int orderId)
        {
            // Move to Accepted
            return UpdateOrderStatus(orderId, "Accepted");
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkReady(int orderId)
        {
            return UpdateOrderStatus(orderId, "Ready for Pickup");
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkPicked(int orderId)
        {
            return UpdateOrderStatus(orderId, "Picked");
        }

        // Toggle flag on order (uses IsFlagged bit/column)
        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleFlag(int orderId)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");
            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string query = @"
            UPDATE OrderTable
            SET IsFlagged = CASE WHEN ISNULL(IsFlagged,0) = 1 THEN 0 ELSE 1 END,
                UpdatedAt = @now
            WHERE OrderId = @id";

                using SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", orderId);
                int rows = cmd.ExecuteNonQuery();

                if (rows == 0) return Json(new { success = false, message = "Order not found" });

                // return the toggled state to the client if needed
                string readQ = "SELECT ISNULL(IsFlagged,0) FROM OrderTable WHERE OrderId = @id";
                using SqlCommand rcmd = new SqlCommand(readQ, con);
                rcmd.Parameters.AddWithValue("@id", orderId);
                int isFlagged = Convert.ToInt32(rcmd.ExecuteScalar() ?? 0);

                return Json(new { success = true, isFlagged = isFlagged == 1 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // helper used by status endpoints
        private IActionResult UpdateOrderStatus(int orderId, string newStatus)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string query = @"UPDATE OrderTable
                         SET Status = @status, UpdatedAt = @now
                         WHERE OrderId = @id";

                using SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@status", newStatus);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", orderId);

                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return Json(new { success = false, message = "Order not found" });

                return Json(new { success = true, newStatus = newStatus });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Optional: minimal OrderDetails action if not present
        [AdminAuthorize]
        [HttpGet]
        public IActionResult OrderDetails(int orderId)
        {
            // You can load detailed data here and pass to the view.
            ViewBag.OrderId = orderId;
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetOrderDetails(int orderId)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Order header + payment + pickup info
                string headerQuery = @"
            SELECT o.OrderId,
                   o.CreatedAt AS OrderDateTime,
                   o.Status,
                   o.CustomerName,
                   o.CustomerPhone,
                   o.PickupSlot,
                   o.TotalCalories,
                   o.DietType,
                   p.PaymentMode,
                   p.Amount,
                   p.IsRefunded,
                   p.RefundStatus,
                   ISNULL(o.IsFlagged,0) AS IsFlagged
            FROM OrderTable o
            LEFT JOIN Payment p ON p.OrderId = o.OrderId
            WHERE o.OrderId = @id";

                using SqlCommand headerCmd = new SqlCommand(headerQuery, con);
                headerCmd.Parameters.AddWithValue("@id", orderId);

                object result = headerCmd.ExecuteScalar();
                var orderObj = new Dictionary<string, object>();

                using (SqlDataReader dr = headerCmd.ExecuteReader())
                {
                    if (!dr.Read())
                        return Json(new { error = "Order not found" });

                    orderObj["OrderId"] = Convert.ToInt32(dr["OrderId"]);
                    orderObj["OrderDateTime"] = dr["OrderDateTime"] == DBNull.Value ? null : dr["OrderDateTime"];
                    orderObj["Status"] = dr["Status"]?.ToString() ?? "";
                    orderObj["CustomerName"] = dr["CustomerName"]?.ToString() ?? "";
                    orderObj["CustomerPhone"] = dr["CustomerPhone"]?.ToString() ?? "";
                    orderObj["PickupSlot"] = dr["PickupSlot"]?.ToString() ?? "";
                    orderObj["TotalCalories"] = dr["TotalCalories"] == DBNull.Value ? 0 : Convert.ToInt32(dr["TotalCalories"]);
                    orderObj["DietType"] = dr["DietType"]?.ToString() ?? "";
                    orderObj["PaymentMode"] = dr["PaymentMode"]?.ToString() ?? "";
                    orderObj["Amount"] = dr["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(dr["Amount"]);
                    orderObj["IsRefunded"] = dr["IsRefunded"] != DBNull.Value && Convert.ToInt32(dr["IsRefunded"]) == 1;
                    orderObj["RefundStatus"] = dr["RefundStatus"]?.ToString() ?? "";
                    orderObj["IsFlagged"] = dr["IsFlagged"] != DBNull.Value && Convert.ToInt32(dr["IsFlagged"]) == 1;
                }

                // Items
                var items = new List<object>();
                string itemsQuery = @"
            SELECT ItemName AS Name, Quantity, Instructions
            FROM OrderItems
            WHERE OrderId = @id";
                using (SqlCommand itemsCmd = new SqlCommand(itemsQuery, con))
                {
                    itemsCmd.Parameters.AddWithValue("@id", orderId);
                    using SqlDataReader dr2 = itemsCmd.ExecuteReader();
                    while (dr2.Read())
                    {
                        items.Add(new
                        {
                            Name = dr2["Name"]?.ToString() ?? "",
                            Quantity = dr2["Quantity"] == DBNull.Value ? 0 : Convert.ToInt32(dr2["Quantity"]),
                            Instructions = dr2["Instructions"]?.ToString() ?? ""
                        });
                    }
                }

                orderObj["Items"] = items;

                // Customer order count
                int historyCount = 0;
                if (!string.IsNullOrEmpty(orderObj["CustomerPhone"]?.ToString()))
                {
                    string countQuery = "SELECT COUNT(1) FROM OrderTable WHERE CustomerPhone = @phone";
                    using (SqlCommand cntCmd = new SqlCommand(countQuery, con))
                    {
                        cntCmd.Parameters.AddWithValue("@phone", orderObj["CustomerPhone"]?.ToString());
                        historyCount = Convert.ToInt32(cntCmd.ExecuteScalar() ?? 0);
                    }
                }
                orderObj["CustomerOrderCount"] = historyCount;

                // Pickup status (determine on server side if needed)
                // For now return a simple PickupStatus derived from order Status and CreatedAt.
                string pickupStatus = "On-time";
                if (orderObj["Status"]?.ToString() == "Picked") pickupStatus = "On-time";
                orderObj["PickupStatus"] = pickupStatus;

                return Json(orderObj);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelOrder(int orderId)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Mark order cancelled
                string q = @"
            UPDATE OrderTable
            SET Status = 'Cancelled', UpdatedAt = @now
            WHERE OrderId = @id";
                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", orderId);

                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return Json(new { success = false, message = "Order not found" });

                // Optionally set payment refund pending flag (if payment exists)
                string payQ = @"
            UPDATE Payment
            SET RefundStatus = 'Refund Pending'
            WHERE OrderId = @id";
                using SqlCommand pCmd = new SqlCommand(payQ, con);
                pCmd.Parameters.AddWithValue("@id", orderId);
                pCmd.ExecuteNonQuery();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TriggerRefund(int orderId)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Simple refund: mark payment as refunded. In real systems, call payment provider.
                string q = @"
            UPDATE Payment
            SET IsRefunded = 1, RefundStatus = 'Refunded', UpdatedAt = @now
            WHERE OrderId = @id";

                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", orderId);

                int rows = cmd.ExecuteNonQuery();
                if (rows == 0)
                {
                    // If no payment row updated, still return success=false so UI can show message
                    return Json(new { success = false, message = "Payment record not found for this order" });
                }

                // Optionally mark order with refund flag or note
                string orderQ = @"
            UPDATE OrderTable
            SET UpdatedAt = @now
            WHERE OrderId = @id";
                using (SqlCommand orderCmd = new SqlCommand(orderQ, con))
                {
                    orderCmd.Parameters.AddWithValue("@now", DateTime.Now);
                    orderCmd.Parameters.AddWithValue("@id", orderId);
                    orderCmd.ExecuteNonQuery();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetOrderStatuses()
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

        // Add these methods inside your existing AdminController class.

        [AdminAuthorize]
        [HttpGet]
        public IActionResult FlaggedOrders()
        {
            // Returns the view; view will load data via AJAX
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetFlaggedOrders()
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            string query = @"
        SELECT o.OrderId,
               ISNULL(o.FlagReason, 'Suspicious') AS FlagReason,
               o.CustomerName,
               o.CustomerPhone,
               ISNULL(p.Amount, 0) AS Amount,
               ISNULL(o.TotalCalories, 0) AS TotalCalories,
               o.Status,
               ISNULL(o.IsResolved, 0) AS IsResolved,
               o.CreatedAt
        FROM OrderTable o
        LEFT JOIN Payment p ON p.OrderId = o.OrderId
        WHERE ISNULL(o.IsFlagged, 0) = 1
        ORDER BY o.CreatedAt DESC";

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                using SqlCommand cmd = new SqlCommand(query, con);
                con.Open();
                using var dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        OrderId = Convert.ToInt32(dr["OrderId"]),
                        FlagReason = dr["FlagReason"]?.ToString() ?? "Suspicious",
                        CustomerName = dr["CustomerName"]?.ToString() ?? "",
                        CustomerPhone = dr["CustomerPhone"]?.ToString() ?? "",
                        Amount = dr["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(dr["Amount"]),
                        TotalCalories = dr["TotalCalories"] == DBNull.Value ? 0 : Convert.ToInt32(dr["TotalCalories"]),
                        Status = dr["Status"]?.ToString() ?? "",
                        IsResolved = dr["IsResolved"] != DBNull.Value && Convert.ToInt32(dr["IsResolved"]) == 1,
                        CreatedAt = dr["CreatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["CreatedAt"])
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }

            return Json(list);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyFlag(int orderId)
        {
            // Approve the flag (mark resolved and clear flag)
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string q = @"
            UPDATE OrderTable
            SET IsFlagged = 0, IsResolved = 1, UpdatedAt = @now
            WHERE OrderId = @id";

                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", orderId);
                int rows = cmd.ExecuteNonQuery();

                if (rows == 0) return Json(new { success = false, message = "Order not found" });
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BlockCustomer(string customerPhone)
        {
            if (string.IsNullOrWhiteSpace(customerPhone))
                return Json(new { success = false, message = "Phone required" });

            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");
            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Try to set a block flag in UserSignup (adjust if your user table differs)
                string q = @"
            UPDATE UserSignup
            SET IsBlocked = 1, UpdatedAt = @now
            WHERE Phone = @phone";

                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@phone", customerPhone);
                int rows = cmd.ExecuteNonQuery();

                if (rows == 0)
                {
                    // If user record not found, still return success:false so UI can show message
                    return Json(new { success = false, message = "Customer record not found" });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetFlaggedCount()
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string q = "SELECT COUNT(1) FROM OrderTable WHERE ISNULL(IsFlagged,0) = 1";
                int count = Convert.ToInt32(new SqlCommand(q, con).ExecuteScalar() ?? 0);
                return Json(new { count });
            }
            catch (Exception ex)
            {
                return Json(new { count = 0, error = ex.Message });
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
        public IActionResult GetPickupSlots(DateTime? date)
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            DateTime targetDate = date?.Date ?? DateTime.Today;

            string slotsQuery = @"
        SELECT SlotId, SlotLabel, FORMAT(StartTime, 'HH:mm') AS StartTime, FORMAT(EndTime, 'HH:mm') AS EndTime,
               Capacity, ISNULL(IsDisabled,0) AS IsDisabled
        FROM PickupSlots
        ORDER BY StartTime";

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                using (SqlCommand cmd = new SqlCommand(slotsQuery, con))
                using (SqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        list.Add(new
                        {
                            SlotId = Convert.ToInt32(dr["SlotId"]),
                            SlotLabel = dr["SlotLabel"]?.ToString() ?? "",
                            StartTime = dr["StartTime"]?.ToString() ?? "",
                            EndTime = dr["EndTime"]?.ToString() ?? "",
                            Capacity = dr["Capacity"] == DBNull.Value ? 0 : Convert.ToInt32(dr["Capacity"]),
                            IsDisabled = Convert.ToInt32(dr["IsDisabled"]) == 1
                        });
                    }
                }

                // compute bookings and blocked state per slot
                for (int i = 0; i < list.Count; i++)
                {
                    dynamic slot = list[i];
                    string countQ = @"
                SELECT COUNT(1) FROM OrderTable
                WHERE PickupSlot = @label
                  AND CAST(CreatedAt AS date) = @date
                  AND Status <> 'Cancelled'";

                    using (SqlCommand ccmd = new SqlCommand(countQ, con))
                    {
                        ccmd.Parameters.AddWithValue("@label", slot.SlotLabel);
                        ccmd.Parameters.AddWithValue("@date", targetDate);
                        int bookings = Convert.ToInt32(ccmd.ExecuteScalar() ?? 0);

                        // check blocked for date
                        string blockQ = "SELECT COUNT(1) FROM SlotBlocks WHERE SlotId = @id AND BlockDate = @date";
                        using SqlCommand bcmd = new SqlCommand(blockQ, con);
                        bcmd.Parameters.AddWithValue("@id", slot.SlotId);
                        bcmd.Parameters.AddWithValue("@date", targetDate);
                        bool isBlocked = Convert.ToInt32(bcmd.ExecuteScalar() ?? 0) > 0;

                        // determine status
                        string status = slot.IsDisabled ? "Disabled" : (bookings >= slot.Capacity ? "Full" : "Open");

                        // replace list entry with enriched object
                        list[i] = new
                        {
                            SlotId = slot.SlotId,
                            SlotLabel = slot.SlotLabel,
                            StartTime = slot.StartTime,
                            EndTime = slot.EndTime,
                            Capacity = slot.Capacity,
                            IsDisabled = slot.IsDisabled,
                            CurrentBookings = bookings,
                            Status = status,
                            IsBlockedForDate = isBlocked
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }

            return Json(list);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateSlotStatus(int slotId, bool isDisabled)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string q = @"UPDATE PickupSlots SET IsDisabled = @val, UpdatedAt = @now WHERE SlotId = @id";
                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@val", isDisabled ? 1 : 0);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", slotId);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return Json(new { success = false, message = "Slot not found" });

                return Json(new { success = true, isDisabled });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateSlotCapacity(int slotId, int capacity)
        {
            if (capacity < 0) return Json(new { success = false, message = "Capacity must be non-negative" });

            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string q = @"UPDATE PickupSlots SET Capacity = @cap, UpdatedAt = @now WHERE SlotId = @id";
                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@cap", capacity);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", slotId);
                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return Json(new { success = false, message = "Slot not found" });

                // return new booking count for client convenience
                string countQ = @"
            SELECT COUNT(1) FROM OrderTable
            WHERE PickupSlot = (SELECT SlotLabel FROM PickupSlots WHERE SlotId = @id)
              AND CAST(CreatedAt AS date) = CAST(GETDATE() AS date)
              AND Status <> 'Cancelled'";
                using SqlCommand ccmd = new SqlCommand(countQ, con);
                ccmd.Parameters.AddWithValue("@id", slotId);
                int bookings = Convert.ToInt32(ccmd.ExecuteScalar() ?? 0);

                return Json(new { success = true, capacity, currentBookings = bookings });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleSlotBlock(int slotId, DateTime date)
        {
            string cs = _configuration.GetConnectionString("DBCS")
                        ?? throw new Exception("DBCS not found");
            DateTime blockDate = date.Date;

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // If already blocked -> remove, otherwise insert
                string existsQ = "SELECT COUNT(1) FROM SlotBlocks WHERE SlotId = @id AND BlockDate = @date";
                using (SqlCommand ex = new SqlCommand(existsQ, con))
                {
                    ex.Parameters.AddWithValue("@id", slotId);
                    ex.Parameters.AddWithValue("@date", blockDate);
                    int exists = Convert.ToInt32(ex.ExecuteScalar() ?? 0);
                    if (exists > 0)
                    {
                        string delQ = "DELETE FROM SlotBlocks WHERE SlotId = @id AND BlockDate = @date";
                        using (SqlCommand del = new SqlCommand(delQ, con))
                        {
                            del.Parameters.AddWithValue("@id", slotId);
                            del.Parameters.AddWithValue("@date", blockDate);
                            del.ExecuteNonQuery();
                        }
                        return Json(new { success = true, message = "Unblocked for date" });
                    }
                    else
                    {
                        string insQ = "INSERT INTO SlotBlocks (SlotId, BlockDate, CreatedAt) VALUES (@id, @date, @now)";
                        using (SqlCommand ins = new SqlCommand(insQ, con))
                        {
                            ins.Parameters.AddWithValue("@id", slotId);
                            ins.Parameters.AddWithValue("@date", blockDate);
                            ins.Parameters.AddWithValue("@now", DateTime.Now);
                            ins.ExecuteNonQuery();
                        }
                        return Json(new { success = true, message = "Blocked for date" });
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetFullSlotCount(DateTime? date)
        {
            string cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
            DateTime targetDate = date?.Date ?? DateTime.Today;

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string q = @"
            SELECT COUNT(1)
            FROM PickupSlots ps
            WHERE ISNULL(ps.IsDisabled,0) = 0
              AND ISNULL(ps.Capacity,0) <= (
                SELECT COUNT(1) FROM OrderTable o
                WHERE o.PickupSlot = ps.SlotLabel
                  AND CAST(o.CreatedAt AS date) = @date
                  AND o.Status <> 'Cancelled'
              )";
                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@date", targetDate);
                int count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                return Json(new { count });
            }
            catch (Exception ex)
            {
                return Json(new { count = 0, error = ex.Message });
            }
        }

        // Add these methods inside the existing AdminController class.

        [AdminAuthorize]
        [HttpGet]
        public IActionResult CancelledOrders()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetCancelledOrders()
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");

            string query = @"
        SELECT o.OrderId,
               o.CreatedAt AS OrderCreatedAt,
               o.CancelledAt,
               ISNULL(o.CancelReason, '') AS CancelReason,
               ISNULL(o.CancelledBy, '') AS CancelledBy,
               ISNULL(p.Amount, 0) AS Amount,
               ISNULL(p.RefundMethod, '') AS RefundMethod,
               ISNULL(p.RefundStatus, '') AS RefundStatus,
               ISNULL(o.AdminNotes, '') AS AdminNotes,
               o.CustomerName,
               o.CustomerPhone
        FROM OrderTable o
        LEFT JOIN Payment p ON p.OrderId = o.OrderId
        WHERE ISNULL(o.Status, '') = 'Cancelled'
        ORDER BY ISNULL(o.CancelledAt, o.CreatedAt) DESC";

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                using SqlCommand cmd = new SqlCommand(query, con);
                con.Open();
                using var dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    list.Add(new
                    {
                        OrderId = Convert.ToInt32(dr["OrderId"]),
                        OrderCreatedAt = dr["OrderCreatedAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["OrderCreatedAt"]),
                        CancelledAt = dr["CancelledAt"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(dr["CancelledAt"]),
                        CancelReason = dr["CancelReason"]?.ToString() ?? "",
                        CancelledBy = dr["CancelledBy"]?.ToString() ?? "",
                        Amount = dr["Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(dr["Amount"]),
                        RefundMethod = dr["RefundMethod"]?.ToString() ?? "",
                        RefundStatus = dr["RefundStatus"]?.ToString() ?? "",
                        AdminNotes = dr["AdminNotes"]?.ToString() ?? "",
                        CustomerName = dr["CustomerName"]?.ToString() ?? "",
                        CustomerPhone = dr["CustomerPhone"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }

            return Json(list);
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateRefundStatus(int orderId, string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return Json(new { success = false, message = "Status required" });

            string cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                string q = @"
            UPDATE Payment
            SET RefundStatus = @status, UpdatedAt = @now
            WHERE OrderId = @id";

                using SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@id", orderId);

                int rows = cmd.ExecuteNonQuery();
                if (rows == 0) return Json(new { success = false, message = "Payment record not found" });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAdminNote(int orderId, string note)
        {
            if (string.IsNullOrWhiteSpace(note)) return Json(new { success = false, message = "Note required" });

            string cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();

                // Append the note with timestamp and admin identifier to AdminNotes column.
                // Adjust as needed if you store notes in a separate table.
                string getQ = "SELECT ISNULL(AdminNotes,'') FROM OrderTable WHERE OrderId = @id";
                using (SqlCommand gcmd = new SqlCommand(getQ, con))
                {
                    gcmd.Parameters.AddWithValue("@id", orderId);
                    string existing = (gcmd.ExecuteScalar() ?? "").ToString();
                    string appended = existing + (existing.Length > 0 ? "\n" : "") + $"[{DateTime.Now:yyyy-MM-dd HH:mm}] Admin: {note}";
                    string upQ = "UPDATE OrderTable SET AdminNotes = @notes, UpdatedAt = @now WHERE OrderId = @id";
                    using (SqlCommand ucmd = new SqlCommand(upQ, con))
                    {
                        ucmd.Parameters.AddWithValue("@notes", appended);
                        ucmd.Parameters.AddWithValue("@now", DateTime.Now);
                        ucmd.Parameters.AddWithValue("@id", orderId);
                        int rows = ucmd.ExecuteNonQuery();
                        if (rows == 0) return Json(new { success = false, message = "Order not found" });
                    }
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult GetCancelledCount()
        {
            string cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");

            try
            {
                using SqlConnection con = new SqlConnection(cs);
                con.Open();
                string q = "SELECT COUNT(1) FROM OrderTable WHERE ISNULL(Status,'') = 'Cancelled' AND ISNULL(Payment.RefundStatus,'') <> 'Completed'";
                // payment join to count only those with pending refunds
                string joinQ = @"
            SELECT COUNT(1)
            FROM OrderTable o
            LEFT JOIN Payment p ON p.OrderId = o.OrderId
            WHERE ISNULL(o.Status,'') = 'Cancelled' AND ISNULL(p.RefundStatus,'') <> 'Completed'";

                using SqlCommand cmd = new SqlCommand(joinQ, con);
                int count = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                return Json(new { count });
            }
            catch (Exception ex)
            {
                return Json(new { count = 0, error = ex.Message });
            }
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult OrderManagement()
        {
            // Returns the OrderManagement view (Views/Admin/OrderManagement.cshtml)
            return View();
        }
        [AdminAuthorize]
        [HttpPost]
        public IActionResult ApproveVendor(int id)
        {
            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                string query = "UPDATE VendorSignup SET IsApproved = 1 WHERE VendorId = @id";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("NewVendorRequest");
        }

        [AdminAuthorize]
        [HttpPost]
        public IActionResult RejectVendor(int id)
        {
            string cs = _configuration.GetConnectionString("DBCS");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                string query = @"UPDATE VendorSignup 
                         SET IsRejected = 1, IsApproved = 0 
                         WHERE VendorId = @id";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("NewVendorRequest");
        }
    }
}


