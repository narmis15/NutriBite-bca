using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
using NUTRIBITE.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 10;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult CouponMisuse()
        {
            return View();
        }

        [HttpGet]
        public IActionResult CalorieAnalytics()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Profile(int? userId)
        {
            ViewBag.UserId = userId;
            return View();
        }

        // INDEX: supports search, status filter and pagination
        [HttpGet]
        public IActionResult Index(string search = "", string status = "All", int page = 1)
        {
            var query = _context.UserSignups.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim();
                query = query.Where(u => u.Name.Contains(s) || u.Email.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(u => (u.Status ?? "Active") == status);
            }

            int total = query.Count();

            var users = query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((Math.Max(1, page) - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // statistics
            ViewBag.TotalUsers = _context.UserSignups.Count();
            ViewBag.ActiveUsers = _context.UserSignups.Count(u => (u.Status ?? "Active") == "Active");
            ViewBag.BlockedUsers = _context.UserSignups.Count(u => u.Status == "Blocked");
            var weekStart = DateTime.Today.AddDays(-7);
            ViewBag.NewUsersThisWeek = _context.UserSignups.Count(u => (u.CreatedAt ?? DateTime.MinValue) >= weekStart);

            ViewBag.Page = Math.Max(1, page);
            ViewBag.PageSize = PageSize;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / PageSize);
            ViewBag.Search = search ?? "";
            ViewBag.Status = status ?? "All";

            return View(users);
        }

        // Blocked users page (server-rendered)
        [HttpGet]
        public IActionResult BlockedUsers()
        {
            var blocked = _context.UserSignups
                .Where(u => u.Status == "Blocked")
                .OrderByDescending(u => u.CreatedAt)
                .ToList();

            return View(blocked);
        }

        // DETAILS
        [HttpGet]
        public IActionResult Details(int id)
        {
            var user = _context.UserSignups.FirstOrDefault(u => u.Id == id);
            if (user == null) return NotFound();
            return View(user);
        }

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

        // BLOCK (GET)
        [HttpGet]
        public IActionResult BlockUser(int id)
        {
            var user = _context.UserSignups.Find(id);
            if (user != null)
            {
                user.Status = "Blocked";
                _context.SaveChanges();
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
            }
            return RedirectToAction("Index");
        }

        // DELETE (GET)
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var user = _context.UserSignups.Find(id);
            if (user != null)
            {
                _context.UserSignups.Remove(user);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        // AJAX endpoints
        [HttpGet]
        public IActionResult GetCalorieAnalyticsData(DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.Today.AddDays(-13);
            var end = to ?? DateTime.Today;

            var entries = _context.DailyCalorieEntries
                .Where(x => x.Date >= start && x.Date <= end)
                .ToList();

            var trend = entries
                .GroupBy(x => x.Date)
                .Select(g => new
                {
                    label = g.Key.ToString("dd MMM"),
                    calories = (int)g.Average(x => x.Calories)
                })
                .OrderBy(x => x.label)
                .ToList();

            int avg = trend.Any() ? (int)trend.Average(x => x.calories) : 0;
            int peak = trend.Any() ? trend.Max(x => x.calories) : 0;
            
            var topConsumers = _context.DailyCalorieEntries
                .Where(x => x.Date >= start && x.Date <= end)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    AvgCalories = (int)g.Average(x => x.Calories)
                })
                .OrderByDescending(x => x.AvgCalories)
                .Take(5)
                .ToList();

            var topConsumerNames = _context.UserSignups
                .Where(u => topConsumers.Select(tc => tc.UserId).Contains(u.Id))
                .ToDictionary(u => u.Id, u => u.Name);

            return Json(new
            {
                averageDaily = avg,
                peakDaily = peak,
                activeCount = entries.Select(x => x.UserId).Distinct().Count(),
                comparisonPercent = 85, // Placeholder for global compliance
                trend = trend,
                topConsumers = topConsumers.Select(tc => new { 
                    name = topConsumerNames.ContainsKey(tc.UserId) ? topConsumerNames[tc.UserId] : "User #" + tc.UserId,
                    avg = tc.AvgCalories
                })
            });
        }

        [HttpGet]
        public IActionResult GetCouponMisuseData()
        {
            var users = _context.UserSignups.Take(10).ToList();
            var rows = users.Select(u => new
            {
                userId = u.Id,
                userName = u.Name,
                couponCode = "SAVE20",
                uses = new Random().Next(1, 10),
                cancels = new Random().Next(0, 5),
                lastUsed = DateTime.Now.AddDays(-new Random().Next(0, 30)),
                misuseScore = new Random().Next(10, 90)
            }).ToList();

            return Json(new
            {
                summary = new { avgMisuseScore = rows.Any() ? (int)rows.Average(r => r.misuseScore) : 0 },
                rows = rows
            });
        }

        [HttpGet]
        public IActionResult GetUserProfileData(int? userId)
        {
            // If no ID, use first user as sample
            var targetId = userId ?? _context.UserSignups.OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefault();
            
            if (targetId == 0) return Json(new { error = "No users found" });

            var user = _context.UserSignups.FirstOrDefault(u => u.Id == targetId);
            if (user == null) return Json(new { error = "User not found" });

            var ordersCount = _context.OrderTables.Count(o => o.UserId == targetId);
            var last7Days = DateTime.Today.AddDays(-6);
            var weeklyEntries = _context.DailyCalorieEntries
                .Where(x => x.UserId == targetId && x.Date >= last7Days)
                .OrderBy(x => x.Date)
                .ToList();

            var trend = Enumerable.Range(0, 7).Select(i => {
                var d = last7Days.AddDays(i);
                var entry = weeklyEntries.FirstOrDefault(e => e.Date == d);
                return new { label = d.ToString("dd MMM"), calories = entry?.Calories ?? 0 };
            }).ToList();

            var survey = _context.HealthSurveys.FirstOrDefault(s => s.UserId == targetId);
            int avg = trend.Any() ? (int)trend.Average(x => x.calories) : 0;

            return Json(new
            {
                userId = targetId,
                name = user.Name,
                email = user.Email,
                phone = user.Phone,
                status = user.Status ?? "Active",
                dietType = survey?.DietaryPreference ?? "Not set",
                ordersCount = ordersCount,
                todayCalories = weeklyEntries.FirstOrDefault(e => e.Date == DateTime.Today)?.Calories ?? 0,
                calorieGoal = (int)(survey?.Bmr ?? 2000),
                adminNote = "No specific alerts for this user.",
                registeredAt = user.CreatedAt,
                weeklyCaloriesAverage = avg,
                weeklyTrend = trend
            });
        }
    }
}
