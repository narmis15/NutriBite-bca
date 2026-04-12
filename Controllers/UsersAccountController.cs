using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using NUTRIBITE.Models;
using NUTRIBITE.ViewModels;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
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

        // ================= UPLOAD PROFILE PICTURE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture)
        {
            var uid = ResolveUserId();
            if (!uid.HasValue) 
                return Json(new { success = false, message = "User not authenticated." });

            if (profilePicture == null || profilePicture.Length == 0)
                return Json(new { success = false, message = "No file uploaded." });

            var user = await _context.UserSignups.FindAsync(uid.Value);
            if (user == null)
                return Json(new { success = false, message = "User not found." });

            var uploadsFolderPath = Path.Combine(_hostingEnvironment.WebRootPath, "ProfilePictures");
            if (!Directory.Exists(uploadsFolderPath))
                Directory.CreateDirectory(uploadsFolderPath);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetExtension(profilePicture.FileName);
            var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(stream);
            }

            user.ProfilePictureUrl = "/ProfilePictures/" + uniqueFileName;
            await _context.SaveChangesAsync();

            return Json(new { success = true, filePath = user.ProfilePictureUrl });
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
                return RedirectToAction("MyProfile", "Home");

            var user = _context.UserSignups.FirstOrDefault(u => u.Id == uid.Value);
            if (user == null)
                return RedirectToAction("Login", "Auth");

            user.Name = model.Name.Trim();
            user.Email = model.Email.Trim();
            if (model.Phone != null) user.Phone = model.Phone.Trim();

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

            return RedirectToAction("MyProfile", "Home");
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