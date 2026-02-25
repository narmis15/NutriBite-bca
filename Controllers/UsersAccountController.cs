using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace NUTRIBITE.Controllers
{
    // Additional account-related endpoints for UsersController (kept as partial to avoid editing existing file)
    public partial class UsersController : Controller
    {
        private readonly IConfiguration _configuration;
        public UsersController(IConfiguration configuration) => _configuration = configuration;

        // GET: /Users/Account
        [HttpGet]
        public IActionResult Account()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
            {
                // Secure access: redirect guests to home (no admin overlap)
                return RedirectToAction("Index", "Home");
            }

            // Pass minimal data to view; page will load details via AJAX
            ViewBag.UserId = uid.Value;
            return View();
        }

        // POST: /Users/UpdateCalorieGoal
        // body: goal (int)
        [HttpPost]
        public IActionResult UpdateCalorieGoal(int goal)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return Json(new { success = false, authenticated = false, message = "Not authenticated." });

            if (goal < 0 || goal > 20000) return Json(new { success = false, message = "Invalid goal value." });

            try
            {
                var cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
                using var con = new SqlConnection(cs);
                con.Open();

                // Best-effort: update CalorieGoal column if present. If column missing, fail gracefully.
                using var cmd = new SqlCommand("UPDATE [Users] SET CalorieGoal = @g WHERE Id = @u", con);
                cmd.Parameters.AddWithValue("@g", goal);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                var rows = cmd.ExecuteNonQuery();

                if (rows > 0)
                    return Json(new { success = true, goal });
                else
                    return Json(new { success = false, message = "Unable to update goal (column may not exist)." });
            }
            catch
            {
                return Json(new { success = false, message = "Server error while updating goal." });
            }
        }

        // GET: /Users/GetMealHistory?days=7
        // Returns user's meals for the last N days (default 7). Guests receive authenticated=false.
        [HttpGet]
        public IActionResult GetMealHistory(int days = 7)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            DateTime end = DateTime.Now.Date;
            DateTime start = end.AddDays(-(Math.Max(1, days) - 1));

            if (!uid.HasValue)
            {
                return Json(new { authenticated = false, history = new object[] { } });
            }

            var cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
            var rowsList = new List<object>();

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();

                using var cmd = new SqlCommand(@"
SELECT m.Id, m.FoodId, ISNULL(f.Name,'') AS FoodName, ISNULL(f.Calories,0) AS Calories, m.Slot, CONVERT(date,m.MealDate) AS MealDay
FROM Meals m
LEFT JOIN Foods f ON f.Id = m.FoodId
WHERE m.UserId = @u AND CONVERT(date,m.MealDate) BETWEEN @start AND @end
ORDER BY MealDay DESC, m.Slot, m.Id", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                cmd.Parameters.AddWithValue("@start", start);
                cmd.Parameters.AddWithValue("@end", end);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    rowsList.Add(new
                    {
                        id = r["Id"],
                        foodId = r["FoodId"],
                        name = r["FoodName"] as string ?? "",
                        calories = r["Calories"] != null ? Convert.ToInt32(r["Calories"]) : 0,
                        slot = r["Slot"] as string ?? "",
                        date = ((DateTime)r["MealDay"]).ToString("yyyy-MM-dd")
                    });
                }

                return Json(new { authenticated = true, history = rowsList });
            }
            catch
            {
                return Json(new { authenticated = true, history = new object[] { } });
            }
        }
        // ================= MY PROFILE =================
        [HttpGet]
        public IActionResult MyProfile()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            ViewBag.UserId = uid.Value;
            return View();
        }

        // ================= MY ORDERS =================
        [HttpGet]
        public IActionResult MyOrders()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            ViewBag.UserId = uid.Value;
            return View();
        }

        // ================= SETTINGS =================
        [HttpGet]
        public IActionResult Settings()
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            return View();
        }
    }
}