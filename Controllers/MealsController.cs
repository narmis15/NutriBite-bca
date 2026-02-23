using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;


namespace NUTRIBITE.Controllers
{
    public class MealsController : Controller
    {
        private readonly IConfiguration _configuration;
        private static readonly string[] ValidSlots = new[] { "Breakfast", "Lunch", "Dinner", "Snacks" };

        public MealsController(IConfiguration configuration) => _configuration = configuration;

        // POST /Meals/Add
        // body: foodId (int), slot (string), date (optional yyyy-MM-dd) - date defaults to server date (local)
        [HttpPost]
        public IActionResult Add(int foodId, string slot, string? date)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            if (!uid.HasValue) return Json(new { success = false, authenticated = false, message = "Not authenticated." });

            if (foodId <= 0) return Json(new { success = false, message = "Invalid food id." });

            if (string.IsNullOrWhiteSpace(slot) || !ValidSlots.Contains(slot, System.StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Invalid meal slot." });

            DateTime mealDate;
            if (!string.IsNullOrWhiteSpace(date))
            {
                if (!DateTime.TryParse(date, out mealDate))
                    mealDate = DateTime.Now.Date;
                else
                    mealDate = mealDate.Date;
            }
            else
            {
                mealDate = DateTime.Now.Date;
            }

            var cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
            try
            {
                using var con = new SqlConnection(cs);
                con.Open();

                // Check for existing entry for same user, food, slot and date
                // NOTE: adjust column names if Meals table differs (MealDate / CreatedAt / Slot)
                using var chk = new SqlCommand(@"
SELECT TOP(1) Id FROM Meals 
WHERE UserId = @u AND FoodId = @f AND Slot = @s AND CONVERT(date, MealDate) = @d", con);
                chk.Parameters.AddWithValue("@u", uid.Value);
                chk.Parameters.AddWithValue("@f", foodId);
                chk.Parameters.AddWithValue("@s", slot);
                chk.Parameters.AddWithValue("@d", mealDate);
                var existing = chk.ExecuteScalar();

                if (existing != null)
                {
                    return Json(new { success = true, added = false, message = "Already added to this slot for the selected date." });
                }

                using var ins = new SqlCommand(@"
INSERT INTO Meals (UserId, FoodId, Slot, MealDate, CreatedAt)
VALUES (@u, @f, @s, @d, GETDATE())", con);
                ins.Parameters.AddWithValue("@u", uid.Value);
                ins.Parameters.AddWithValue("@f", foodId);
                ins.Parameters.AddWithValue("@s", slot);
                ins.Parameters.AddWithValue("@d", mealDate);

                ins.ExecuteNonQuery();

                return Json(new { success = true, added = true, slot = slot, date = mealDate.ToString("yyyy-MM-dd") });
            }
            catch
            {
                return Json(new { success = false, message = "Server error while saving meal." });
            }
        }

        // GET /Meals/Today?date=yyyy-MM-dd
        // Returns authenticated flag, list of meals for the user for the given date (defaults to today),
        // total calories, calorie goal (if available) and remaining calories.
        [HttpGet]
        public IActionResult Today(string? date)
        {
            var uid = HttpContext.Session.GetInt32("UserId");
            DateTime mealDate;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsed))
                mealDate = parsed.Date;
            else
                mealDate = DateTime.Now.Date;

            if (!uid.HasValue)
            {
                return Json(new
                {
                    authenticated = false,
                    date = mealDate.ToString("yyyy-MM-dd"),
                    meals = new object[] { },
                    totalCalories = 0,
                    calorieGoal = (int?)null,
                    remaining = (int?)null
                });
            }


            var cs = _configuration.GetConnectionString("DBCS") ?? throw new Exception("DBCS not found");
            var meals = new List<object>();
            int totalCalories = 0;
            int? calorieGoal = null;

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();

                // join meals with foods to obtain name and calories (adjust column names if your schema differs)
                using var cmd = new SqlCommand(@"
SELECT m.Id, m.FoodId, ISNULL(f.Name, '') AS FoodName, 
       ISNULL(f.Calories, 0) AS Calories, m.Slot, m.MealDate
FROM Meals m
LEFT JOIN Foods f ON f.Id = m.FoodId
WHERE m.UserId = @u AND CONVERT(date, m.MealDate) = @d
ORDER BY m.Slot, m.Id", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                cmd.Parameters.AddWithValue("@d", mealDate);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var caloriesObj = r["Calories"];
                    int cal = 0;
                    if (caloriesObj != null && int.TryParse(caloriesObj.ToString(), out var c)) cal = c;
                    totalCalories += cal;

                    meals.Add(new
                    {
                        id = r["Id"],
                        foodId = r["FoodId"],
                        name = r["FoodName"] as string ?? "",
                        calories = cal,
                        slot = r["Slot"] as string ?? "",
                        mealDate = ((DateTime)r["MealDate"]).ToString("yyyy-MM-dd")
                    });
                }

                // attempt to read per-user calorie goal if available
                try
                {
                    using var gcmd = new SqlCommand("SELECT TOP(1) CalorieGoal FROM [Users] WHERE Id = @u", con);
                    gcmd.Parameters.AddWithValue("@u", uid.Value);
                    var gres = gcmd.ExecuteScalar();
                    if (gres != null && int.TryParse(gres.ToString(), out var gval)) calorieGoal = gval;
                }
                catch
                {
                    // ignore if column doesn't exist or other issues — calorieGoal remains null
                }

                int? remaining = calorieGoal.HasValue ? calorieGoal.Value - totalCalories : (int?)null;

                return Json(new
                {
                    authenticated = true,
                    date = mealDate.ToString("yyyy-MM-dd"),
                    meals,
                    totalCalories,
                    calorieGoal,
                    remaining
                });
            }
            catch
            {
                return Json(new
                {
                    authenticated = true,
                    date = mealDate.ToString("yyyy-MM-dd"),
                    meals = new object[] { },
                    totalCalories = 0,
                    calorieGoal = (int?)null,
                    remaining = (int?)null
                });
            }
        }
    
    
       [HttpGet]
        public IActionResult Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Json(new object[] { });

            var cs = _configuration.GetConnectionString("DBCS")
                     ?? throw new Exception("DBCS not found");

            var results = new List<object>();

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();

                using var cmd = new SqlCommand(@"
SELECT TOP 50 Id, Name, Description, Image, Calories
FROM Foods
WHERE Name LIKE @q
ORDER BY Name", con);

                cmd.Parameters.AddWithValue("@q", "%" + q + "%");

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    results.Add(new
                    {
                        id = reader["Id"],
                        name = reader["Name"]?.ToString() ?? "",
                        description = reader["Description"]?.ToString() ?? "",
                        image = reader["Image"]?.ToString() ?? "/images/placeholder.png",
                        calories = reader["Calories"] ?? 0
                    });
                }

                return Json(results);
            }
            catch
            {
                return Json(new object[] { });
            }
        }
    }
}
