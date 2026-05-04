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

using System.Security.Cryptography;
using System.Text;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
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

        // ================= UPDATE PASSWORD =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var uid = ResolveUserId();
            if (!uid.HasValue) return Json(new { success = false, message = "Not authenticated." });

            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                return Json(new { success = false, message = "All fields are required." });

            if (newPassword != confirmPassword)
                return Json(new { success = false, message = "New password and confirm password do not match." });

            var user = await _context.UserSignups.FindAsync(uid.Value);
            if (user == null) return Json(new { success = false, message = "User not found." });

            if (user.Password != HashPassword(currentPassword))
                return Json(new { success = false, message = "Current password is incorrect." });

            // Basic strength check (ideally share this logic)
            if (newPassword.Length < 8 || !newPassword.Any(char.IsUpper) || !newPassword.Any(char.IsLower) || !newPassword.Any(char.IsDigit))
                return Json(new { success = false, message = "Password must be at least 8 chars long with uppercase, lowercase, and a number." });

            user.Password = HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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

            // Auto-generate daily meal records from active subscriptions
            var activeSubscriptions = _context.Subscriptions
                .Where(s => s.UserId == uid.Value && s.Status == "Active")
                .ToList();

            foreach (var sub in activeSubscriptions)
            {
                if (!sub.FoodId.HasValue) continue;

                var subStart = sub.StartDate.Date;
                var subEnd = sub.EndDate?.Date ?? end;

                // For each day in the requested range that falls within the subscription period
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    if (date >= subStart && date <= subEnd)
                    {
                        // Check if a meal record already exists for this subscription on this day
                        bool exists = _context.Meals.Any(m => 
                            m.UserId == uid.Value && 
                            m.FoodId == sub.FoodId.Value && 
                            m.MealDate.Date == date);

                        if (!exists)
                        {
                            _context.Meals.Add(new Meal
                            {
                                UserId = uid.Value,
                                FoodId = sub.FoodId.Value,
                                MealDate = date,
                                Slot = "Subscription Delivery",
                                CreatedAt = DateTime.Now
                            });
                        }
                    }
                }
            }
            
            // Save the newly generated history records
            if (_context.ChangeTracker.HasChanges())
            {
                _context.SaveChanges();
            }

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

            // Notify Admin via System Log (ActivityLog) BEFORE deletion so info is available
            var log = new ActivityLog
            {
                Action = "Account Deleted",
                Details = $"User {user.Name} ({user.Email}) has permanently deleted their account and all associated data.",
                Timestamp = DateTime.Now,
                AdminEmail = "admin@nutribite.com",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            };
            _context.ActivityLogs.Add(log);

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

            return RedirectToAction("Index", "Public");
        }

        // ================= STATIC VIEWS =================
        public IActionResult MyProfile() => View();
        public IActionResult MyOrders() => View();
        public IActionResult Settings() => View();
    }
}