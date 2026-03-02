using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using NUTRIBITE.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
        private readonly IConfiguration _configuration;
        public UsersController(IConfiguration configuration) => _configuration = configuration;

        // Simplified ResolveUserId: session-only (no claims/cookie fallbacks).
        private int? ResolveUserId()
        {
            return HttpContext?.Session?.GetInt32("UserId");
        }

        // GET: /Users/Account
        [HttpGet]
        public IActionResult Account()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            ViewBag.UserId = uid.Value;
            return View();
        }

        // NEW: GET: /Users/GetUserProfileData
        [HttpGet]
        public IActionResult GetUserProfileData()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue) return Json(new { authenticated = false });

            var cs = _configuration.GetConnectionString("DBCS");
            object? userRow = null;
            object? surveyObj = null;

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();

                using var cmd = new SqlCommand("SELECT TOP 1 Id, Name, Email, Phone FROM UserSignup WHERE Id = @u", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    userRow = new
                    {
                        Id = Convert.ToInt32(r["Id"]),
                        Name = r["Name"] as string ?? "",
                        Email = r["Email"] as string ?? "",
                        Phone = r["Phone"] as string ?? ""
                    };
                }
                r.Close();

                using var cmd2 = new SqlCommand("SELECT TOP 1 * FROM HealthSurveys WHERE UserId = @u ORDER BY CreatedAt DESC", con);
                cmd2.Parameters.AddWithValue("@u", uid.Value);
                using var r2 = cmd2.ExecuteReader();
                if (r2.Read())
                {
                    surveyObj = new
                    {
                        Id = Convert.ToInt32(r2["Id"]),
                        Age = r2["Age"] == DBNull.Value ? 0 : Convert.ToInt32(r2["Age"]),
                        Gender = r2["Gender"]?.ToString() ?? "",
                        HeightCm = r2["HeightCm"] == DBNull.Value ? 0m : Convert.ToDecimal(r2["HeightCm"]),
                        WeightKg = r2["WeightKg"] == DBNull.Value ? 0m : Convert.ToDecimal(r2["WeightKg"]),
                        ActivityLevel = r2["ActivityLevel"]?.ToString() ?? "",
                        Goal = r2["Goal"]?.ToString() ?? "",
                        ChronicDiseases = r2["ChronicDiseases"]?.ToString() ?? "",
                        DietaryPreference = r2["DietaryPreference"]?.ToString() ?? "",
                        BMI = r2["BMI"] == DBNull.Value ? 0m : Convert.ToDecimal(r2["BMI"]),
                        RecommendedCalories = r2["RecommendedCalories"] == DBNull.Value ? 0 : Convert.ToInt32(r2["RecommendedCalories"]),
                        RecommendedProtein = r2["RecommendedProtein"] == DBNull.Value ? 0 : Convert.ToInt32(r2["RecommendedProtein"])
                    };
                }
            }
            catch
            {
                // fail-open: return what we have
            }

            return Json(new { authenticated = true, user = userRow, survey = surveyObj });
        }

        // NEW: GET: /Users/Edit
        [HttpGet]
        public IActionResult Edit()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Auth");

            var cs = _configuration.GetConnectionString("DBCS");
            var vm = new UserProfileEditViewModel();

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand("SELECT TOP 1 Name, Email FROM UserSignup WHERE Id = @u", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    vm.Name = r["Name"] as string ?? "";
                    vm.Email = r["Email"] as string ?? "";
                }
            }
            catch
            {
                // ignore, return empty vm
            }

            return View(vm);
        }

        // NEW: POST: /Users/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserProfileEditViewModel model)
        {
            var uid = ResolveUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid) return View(model);

            var cs = _configuration.GetConnectionString("DBCS");

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand("UPDATE UserSignup SET Name = @n, Email = @e WHERE Id = @u", con);
                cmd.Parameters.AddWithValue("@n", model.Name.Trim());
                cmd.Parameters.AddWithValue("@e", model.Email.Trim());
                cmd.Parameters.AddWithValue("@u", uid.Value);
                cmd.ExecuteNonQuery();
            }
            catch
            {
                ModelState.AddModelError("", "Unable to update profile. Please try again later.");
                return View(model);
            }

            // If ASP.NET Identity user exists with same email, attempt safe email update
            try
            {
                var userManager = HttpContext.RequestServices.GetService(typeof(UserManager<Microsoft.AspNetCore.Identity.IdentityUser>))
                    as UserManager<Microsoft.AspNetCore.Identity.IdentityUser>;
                if (userManager != null)
                {
                    var idUser = await userManager.FindByEmailAsync(model.Email.Trim());
                    if (idUser != null)
                    {
                        idUser.UserName = model.Name?.Trim();
                        idUser.Email = model.Email?.Trim();
                        await userManager.UpdateAsync(idUser);
                    }
                }
            }
            catch
            {
                // ignore identity update errors
            }

            // update session
            try
            {
                var session = HttpContext?.Session;
                if (session != null) session.SetString("UserName", model.Name ?? "");
            }
            catch
            {
                // ignore
            }

            return RedirectToAction("Account");
        }

        // POST: /Users/UpdateCalorieGoal
        // body: goal (int)
        [HttpPost]
        public IActionResult UpdateCalorieGoal(int goal)
        {
            var uid = ResolveUserId();
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
            var uid = ResolveUserId();
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
        public IActionResult MyProfile(int? userId = null)
        {
            // If user is logged in, show their profile
            var uid = ResolveUserId();
            if (uid.HasValue)
            {
                ViewBag.UserId = uid.Value;
                return View("MyProfile");
            }

            // If caller provided a userId query (public profile view), show that profile without session
            if (userId.HasValue)
            {
                ViewBag.UserId = userId.Value;
                return View("MyProfile"); // or return View("MyProfile") if that view expects only ViewBag.UserId
            }

            // No session and no userId -> redirect to Login
            return RedirectToAction("Login", "Auth");
        }

        // ================= MY ORDERS =================
        [HttpGet]
        public IActionResult MyOrders()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            ViewBag.UserId = uid.Value;
            return View();
        }

        // ================= SETTINGS =================
        [HttpGet]
        public IActionResult Settings()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            return View();
        }

        // ================= DELETE ACCOUNT =================
        // POST: /Users/DeleteAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Auth");

            var cs = _configuration.GetConnectionString("DBCS");
            string? userEmail = null;

            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var cmd = new SqlCommand("SELECT Email FROM UserSignup WHERE Id = @u", con);
                cmd.Parameters.AddWithValue("@u", uid.Value);
                userEmail = cmd.ExecuteScalar() as string;
            }
            catch
            {
                // ignore
            }

            // Try delete Identity user if present
            try
            {
                var userManager = HttpContext.RequestServices.GetService(typeof(UserManager<Microsoft.AspNetCore.Identity.IdentityUser>)) as UserManager<Microsoft.AspNetCore.Identity.IdentityUser>;
                if (userManager != null && !string.IsNullOrWhiteSpace(userEmail))
                {
                    var idUser = await userManager.FindByEmailAsync(userEmail);
                    if (idUser != null)
                    {
                        await userManager.DeleteAsync(idUser);
                    }
                }
            }
            catch
            {
                // ignore identity deletion errors
            }

            // Delete related records in app DB (HealthSurveys, WeightProgress, DailyCalorieEntry and UserSignup)
            try
            {
                using var con = new SqlConnection(cs);
                con.Open();
                using var tx = con.BeginTransaction();
                try
                {
                    using var cmd1 = new SqlCommand("DELETE FROM HealthSurveys WHERE UserId = @u", con, tx);
                    cmd1.Parameters.AddWithValue("@u", uid.Value);
                    cmd1.ExecuteNonQuery();

                    using var cmd2 = new SqlCommand("DELETE FROM WeightProgress WHERE UserId = @u", con, tx);
                    cmd2.Parameters.AddWithValue("@u", uid.Value);
                    cmd2.ExecuteNonQuery();

                    using var cmd3 = new SqlCommand("DELETE FROM DailyCalorieEntry WHERE UserId = @u", con, tx);
                    cmd3.Parameters.AddWithValue("@u", uid.Value);
                    cmd3.ExecuteNonQuery();

                    using var cmd4 = new SqlCommand("DELETE FROM UserSignup WHERE Id = @u", con, tx);
                    cmd4.Parameters.AddWithValue("@u", uid.Value);
                    cmd4.ExecuteNonQuery();

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                }
            }
            catch
            {
                // ignore
            }

            // Sign out / clear session
            try
            {
                var signInMgr = HttpContext.RequestServices.GetService(typeof(SignInManager<Microsoft.AspNetCore.Identity.IdentityUser>)) as SignInManager<Microsoft.AspNetCore.Identity.IdentityUser>;
                if (signInMgr != null) await signInMgr.SignOutAsync();
            }
            catch { }

            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // existing actions remain unchanged...
    }
}