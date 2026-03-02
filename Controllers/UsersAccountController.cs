using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NUTRIBITE.Models;
using NUTRIBITE.ViewModels;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
        private readonly UserManager<IdentityUser>? _userManager;
        private readonly SignInManager<IdentityUser>? _signInManager;

        public UsersController(
            ApplicationDbContext context,
            UserManager<IdentityUser>? userManager = null,
            SignInManager<IdentityUser>? signInManager = null)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        private int? ResolveUserId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        // ================= ACCOUNT =================
        [HttpGet]
        public IActionResult Account()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            ViewBag.UserId = uid.Value;
            return View();
        }

        // ================= GET PROFILE DATA =================
        [HttpGet]
        public IActionResult GetUserProfileData()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return Json(new { authenticated = false });

            var user = _context.UserSignups
                .Where(u => u.Id == uid.Value)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Phone
                })
                .FirstOrDefault();

            var survey = _context.HealthSurveys
                .Where(h => h.UserId == uid.Value)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new
                {
                    h.Id,
                    h.Age,
                    h.Gender,
                    h.HeightCm,
                    h.WeightKg,
                    h.ActivityLevel,
                    h.Goal,
                    h.ChronicDiseases,
                    h.DietaryPreference,
                    BMI = h.Bmi,
                    h.RecommendedCalories,
                    h.RecommendedProtein
                })
                .FirstOrDefault();

            return Json(new { authenticated = true, user, survey });
        }

        // ================= EDIT PROFILE =================
        [HttpGet]
        public IActionResult Edit()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups
                .Where(u => u.Id == uid.Value)
                .Select(u => new UserProfileEditViewModel
                {
                    Name = u.Name,
                    Email = u.Email
                })
                .FirstOrDefault();

            return View(user ?? new UserProfileEditViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserProfileEditViewModel model)
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
                return View(model);

            var user = _context.UserSignups.FirstOrDefault(u => u.Id == uid.Value);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            user.Name = model.Name.Trim();
            user.Email = model.Email.Trim();

            _context.SaveChanges();

            HttpContext.Session.SetString("UserName", user.Name);

            // Optional Identity update
            if (_userManager != null)
            {
                var idUser = await _userManager.FindByEmailAsync(model.Email);
                if (idUser != null)
                {
                    idUser.UserName = model.Name;
                    idUser.Email = model.Email;
                    await _userManager.UpdateAsync(idUser);
                }
            }

            return RedirectToAction("Account");
        }

        // ================= UPDATE CALORIE GOAL =================
        [HttpPost]
        public IActionResult UpdateCalorieGoal(int goal)
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return Json(new { success = false, authenticated = false });

            var user = _context.UserSignups.FirstOrDefault(u => u.Id == uid.Value);
            if (user == null)
                return Json(new { success = false });

            user.CalorieGoal = goal;
            _context.SaveChanges();

            return Json(new { success = true, goal });
        }

        // ================= MEAL HISTORY =================
        [HttpGet]
        public IActionResult GetMealHistory(int days = 7)
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return Json(new { authenticated = false });

            DateTime end = DateTime.Today;
            DateTime start = end.AddDays(-(Math.Max(1, days) - 1));

            var history = _context.Meals
                .Where(m => m.UserId == uid.Value &&
                            m.MealDate >= start &&
                            m.MealDate <= end)
                .Join(_context.Foods,
                      m => m.FoodId,
                      f => f.Id,
                      (m, f) => new
                      {
                          id = m.Id,
                          foodId = f.Id,
                          name = f.Name,
                          calories = f.Calories ?? 0,
                          slot = m.Slot,
                          date = m.MealDate.ToString("yyyy-MM-dd")
                      })
                .OrderByDescending(x => x.date)
                .ToList();

            return Json(new { authenticated = true, history });
        }

        // ================= DELETE ACCOUNT =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var uid = ResolveUserId();
            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = _context.UserSignups.FirstOrDefault(u => u.Id == uid.Value);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            var surveys = _context.HealthSurveys.Where(h => h.UserId == uid.Value);
            var entries = _context.DailyCalorieEntries.Where(d => d.UserId == uid.Value);
            var meals = _context.Meals.Where(m => m.UserId == uid.Value);

            _context.HealthSurveys.RemoveRange(surveys);
            _context.DailyCalorieEntries.RemoveRange(entries);
            _context.Meals.RemoveRange(meals);
            _context.UserSignups.Remove(user);

            await _context.SaveChangesAsync();

            if (_signInManager != null)
                await _signInManager.SignOutAsync();

            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        // ================= STATIC VIEWS =================
        public IActionResult MyProfile() => View();
        public IActionResult MyOrders() => View();
        public IActionResult Settings() => View();
    }
}