using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using global::NUTRIBITE.Models;
using global::NUTRIBITE.Models.Users;
using global::NUTRIBITE.Filters;
using global::NUTRIBITE.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using global::NUTRIBITE.Services;
using Microsoft.AspNetCore.Identity;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser>? _userManager;
        private readonly SignInManager<IdentityUser>? _signInManager;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IActivityLogger _activityLogger;
        private const int PageSize = 10;

        [ActivatorUtilitiesConstructor]
        public UsersController(
            ApplicationDbContext context,
            IWebHostEnvironment hostingEnvironment,
            IActivityLogger activityLogger,
            UserManager<IdentityUser>? userManager = null,
            SignInManager<IdentityUser>? signInManager = null)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _activityLogger = activityLogger;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // INDEX: supports search, status filter and pagination
        // EDIT - GET
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var user = _context.UserSignups.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound();
            return View(user);
        }

        // EDIT - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, string Name, string Email, string? Phone, string? Status)
        {
            var user = _context.UserSignups.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound();

            user.Name = Name ?? user.Name;
            user.Email = Email ?? user.Email;
            user.Phone = Phone;
            user.Status = string.IsNullOrWhiteSpace(Status) ? user.Status : Status;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // BLOCK (GET) - simple admin action (keeps UI navigation simple)
        [HttpGet]
        public IActionResult BlockUser(int id)
        {
            var user = _context.UserSignups.Find(id);
            if (user != null)
            {
                user.Status = "Blocked";
                _context.SaveChanges();
                _activityLogger.LogAsync("User Blocked", $"User {user.Email} (ID: {id}) was blocked.");
                TempData["Success"] = $"User {user.Name} blocked successfully.";
            }
            return RedirectToAction("Index");
        }

        // UNBLOCK (GET)
        [HttpGet]
        public IActionResult UnblockUser(int id)
        {
            var user = _context.UserSignups.Find(id);
            if (user != null)
            {
                user.Status = "Active";
                _context.SaveChanges();
                _activityLogger.LogAsync("User Unblocked", $"User {user.Email} (ID: {id}) was unblocked.");
                TempData["Success"] = $"User {user.Name} unblocked successfully.";
            }
            return RedirectToAction("Index");
        }

        // DELETE (GET) - confirmation should be client-side; server removes record
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var user = _context.UserSignups.Find(id);
            if (user != null)
            {
                var email = user.Email;
                _context.UserSignups.Remove(user);
                _context.SaveChanges();
                _activityLogger.LogAsync("User Deleted", $"User {email} (ID: {id}) was deleted.");
                TempData["Success"] = $"User {email} deleted successfully.";
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        [AdminAuthorize]
        public IActionResult Index(string search = "", string status = "All", int page = 1)
        {
            try
            {
                var query = _context.UserSignups.AsQueryable();

                // 1. Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string s = search.Trim().ToLower();
                    query = query.Where(u => u.Name.ToLower().Contains(s) || u.Email.ToLower().Contains(s) || (u.Phone != null && u.Phone.Contains(s)));
                }

                // 2. Status Filter
                if (!string.IsNullOrWhiteSpace(status) && status != "All")
                {
                    query = query.Where(u => (u.Status ?? "Active") == status);
                }

                // 3. Pagination & Execution
                int total = query.Count();
                int pageSize = 10;
                int totalPages = (int)Math.Ceiling((double)total / pageSize);
                int currentPage = Math.Max(1, page);

                var users = query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((currentPage - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // 4. Statistics for the cards
                ViewBag.TotalUsers = _context.UserSignups.Count();
                ViewBag.ActiveUsers = _context.UserSignups.Count(u => (u.Status ?? "Active") == "Active");
                ViewBag.BlockedUsers = _context.UserSignups.Count(u => u.Status == "Blocked");
                ViewBag.DeletedUsers = _context.UserSignups.Count(u => u.Status == "Deleted");
                
                var weekAgo = DateTime.Today.AddDays(-7);
                ViewBag.NewUsersThisWeek = _context.UserSignups.Count(u => (u.CreatedAt ?? DateTime.MinValue) >= weekAgo);

                // 5. View Data
                ViewBag.Search = search;
                ViewBag.Status = status;
                ViewBag.Page = currentPage;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = total;

                return View(users);
            }
            catch (Exception ex)
            {
                // Log the error and show a user-friendly message
                _activityLogger.LogAsync("Error", $"Failed to load user index: {ex.Message}");
                TempData["Error"] = "An error occurred while loading users. Please try again.";
                return View(new List<UserSignup>());
            }
        }

        // Blocked users page (server-rendered)
        [HttpGet]
        [AdminAuthorize]
        public IActionResult BlockedUsers()
        {
            try
            {
                // Return list of blocked users to the BlockedUsers.cshtml view
                var blocked = _context.UserSignups
                    .Where(u => u.Status == "Blocked")
                    .OrderByDescending(u => u.CreatedAt)
                    .ToList();

                return View(blocked);
            }
            catch (Exception ex)
            {
                _activityLogger.LogAsync("Error", $"Failed to load blocked users: {ex.Message}");
                TempData["Error"] = "Error loading blocked users.";
                return View(new List<UserSignup>());
            }
        }

        // DETAILS
        [HttpGet]
        public IActionResult Details(int id)
        {
            var user = _context.UserSignups.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound();

            var orders = _context.OrderTables.Where(o => o.UserId == id).ToList();
            var activityHistory = _context.ActivityLogs.AsEnumerable().Where(a => a.Details != null && a.Details.Contains($"(ID: {id})")).ToList();
            var healthSurvey = _context.HealthSurveys.FirstOrDefault(h => h.UserId == id);

            var viewModel = new UserDetailsViewModel
            {
                User = user,
                Orders = orders,
                ActivityHistory = activityHistory,
                HealthSurvey = healthSurvey ?? new HealthSurvey()
            };

            return View(viewModel);
        }

        // PROFILE VIEW
        [HttpGet]
        [AdminAuthorize]
        public IActionResult Profile(int userId)
        {
            var user = _context.UserSignups.Find(userId);
            if (user == null) return NotFound();
            ViewBag.UserId = userId;
            return View();
        }

        // CALORIE ANALYTICS DATA API
        [HttpGet]
        [AdminAuthorize]
        public async Task<IActionResult> GetCalorieAnalyticsData(DateTime? from, DateTime? to, int? userId = null)
        {
            try
            {
                var startDate = from ?? DateTime.Today.AddDays(-13);
                var endDate = to ?? DateTime.Today;

                var query = _context.DailyCalorieEntries.AsQueryable();
                
                bool isGlobal = false;
                if (userId.HasValue)
                {
                    query = query.Where(e => e.UserId == userId.Value);
                }
                else
                {
                    isGlobal = true;
                }

                var entries = await query
                    .Where(e => e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                // 1. Trend Data
                var trend = Enumerable.Range(0, (endDate - startDate).Days + 1)
                    .Select(i => startDate.AddDays(i))
                    .Select(d => new
                    {
                        label = d.ToString("dd MMM"),
                        calories = entries.Where(e => e.Date.Date == d.Date).Sum(e => e.Calories)
                    })
                    .ToList();

                // 2. Meal Breakdown
                var mealBreakdown = entries
                    .GroupBy(e => e.MealType ?? "Other")
                    .Select(g => new { label = g.Key, value = g.Sum(e => e.Calories) })
                    .ToList();

                // 3. Stats
                var totalCalories = entries.Sum(e => e.Calories);
                var distinctDays = entries.Select(e => e.Date.Date).Distinct().Count();
                var averageDaily = distinctDays > 0 ? (int)(totalCalories / distinctDays) : 0;
                
                var peakDaily = entries.GroupBy(e => e.Date.Date)
                    .Select(g => g.Sum(e => e.Calories))
                    .DefaultIfEmpty(0)
                    .Max();

                var todayCalories = entries.Where(e => e.Date.Date == DateTime.Today)
                    .Sum(e => e.Calories);

                // Goal data
                int recommendedDaily = 2000;
                if (!isGlobal && userId.HasValue)
                {
                    var survey = await _context.HealthSurveys.FirstOrDefaultAsync(s => s.UserId == userId.Value);
                    if (survey?.Bmr > 0) recommendedDaily = (int)survey.Bmr;
                }

                var comparisonPercent = recommendedDaily > 0 
                    ? Math.Min(100, (int)((double)averageDaily / recommendedDaily * 100))
                    : 0;

                return Json(new
                {
                    success = true,
                    trend,
                    mealBreakdown,
                    averageDaily,
                    peakDaily,
                    todayCalories,
                    recommendedDaily,
                    comparisonPercent,
                    isGlobal
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetUserProfileData(int? userId = null)
        {
            try
            {
                int? targetId = userId;
                var adminEmail = HttpContext.Session.GetString("Admin");
                
                if (!targetId.HasValue)
                {
                    targetId = HttpContext.Session.GetInt32("UserId");
                }
                else if (string.IsNullOrEmpty(adminEmail))
                {
                    // Only admin can request other user's data
                    return Unauthorized();
                }

                if (!targetId.HasValue) return Json(new { success = false, message = "No user ID specified" });

                var user = _context.UserSignups.Find(targetId.Value);
                if (user == null) return NotFound();

                var survey = _context.HealthSurveys
                    .Where(h => h.UserId == targetId.Value)
                    .OrderByDescending(h => h.CreatedAt)
                    .FirstOrDefault();

                var today = DateTime.Today;
                var todayCals = _context.DailyCalorieEntries
                    .Where(d => d.UserId == targetId.Value && d.Date == today)
                    .Sum(d => d.Calories);

                var weekAgo = today.AddDays(-6);
                var weeklyEntries = _context.DailyCalorieEntries
                    .Where(d => d.UserId == targetId.Value && d.Date >= weekAgo)
                    .ToList();

                var weeklyAvg = weeklyEntries.Any() ? (int)weeklyEntries.Average(e => e.Calories) : 0;
                
                var trend = Enumerable.Range(0, 7)
                    .Select(i => weekAgo.AddDays(i))
                    .Select(d => new
                    {
                        Label = d.ToString("dd MMM"),
                        Calories = weeklyEntries.Where(e => e.Date == d).Sum(e => e.Calories)
                    }).ToArray();

                var ordersCount = _context.OrderTables.Count(o => o.UserId == targetId.Value);

                return Json(new
                {
                    success = true,
                    authenticated = true,
                    UserId = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Phone = user.Phone ?? "-",
                    Status = user.Status ?? "Active",
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    RegisteredAt = user.CreatedAt ?? DateTime.MinValue,
                    DietType = survey?.DietaryPreference ?? "Veg",
                    CalorieGoal = (int)(survey?.Bmr ?? 2000),
                    TodayCalories = todayCals,
                    WeeklyCaloriesAverage = weeklyAvg,
                    OrdersCount = ordersCount,
                    WeeklyTrend = trend,
                    AdminNote = "", // Placeholder for future feature
                    Survey = survey != null ? new {
                        survey.Age,
                        survey.Gender,
                        survey.HeightCm,
                        survey.WeightKg,
                        survey.ActivityLevel,
                        survey.Goal,
                        survey.ChronicDiseases,
                        survey.FoodAllergies,
                        survey.DietaryPreference,
                        survey.Bmi,
                        survey.Bmr
                    } : null
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading profile: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize]
        public IActionResult UpdateUserProfile(int userId, string name, string email, string phone, string status, HealthSurvey surveyData)
        {
            try
            {
                var adminEmail = HttpContext.Session.GetString("Admin");
                if (string.IsNullOrEmpty(adminEmail)) return Unauthorized();

                var user = _context.UserSignups.Find(userId);
                if (user == null) return NotFound();

                user.Name = name;
                user.Email = email;
                user.Phone = phone;
                user.Status = status;

                var survey = _context.HealthSurveys.FirstOrDefault(h => h.UserId == userId);
                if (survey == null)
                {
                    survey = new HealthSurvey { UserId = userId, CreatedAt = DateTime.Now };
                    _context.HealthSurveys.Add(survey);
                }

                survey.Age = surveyData.Age;
                survey.Gender = surveyData.Gender;
                survey.HeightCm = surveyData.HeightCm;
                survey.WeightKg = surveyData.WeightKg;
                survey.ActivityLevel = surveyData.ActivityLevel;
                survey.Goal = surveyData.Goal;
                survey.DietaryPreference = surveyData.DietaryPreference;
                survey.ChronicDiseases = surveyData.ChronicDiseases;
                survey.FoodAllergies = surveyData.FoodAllergies;

                // Recalculate BMR/BMI if needed
                if (survey.HeightCm > 0 && survey.WeightKg > 0)
                {
                    decimal heightM = survey.HeightCm.Value / 100m;
                    survey.Bmi = Math.Round(survey.WeightKg.Value / (heightM * heightM), 2);
                    
                    // Simple BMR (Mifflin-St Jeor)
                    if (survey.Gender == "Male")
                        survey.Bmr = (10 * survey.WeightKg) + (6.25m * survey.HeightCm) - (5 * survey.Age) + 5;
                    else
                        survey.Bmr = (10 * survey.WeightKg) + (6.25m * survey.HeightCm) - (5 * survey.Age) - 161;
                }

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Update failed: " + ex.Message });
            }
        }
    }
}