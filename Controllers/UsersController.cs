using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Collections.Generic;
using NUTRIBITE.Models;
using NUTRIBITE.Models.Users;

namespace NUTRIBITE.Controllers
{
    public partial class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================= USERS LIST =================
        [HttpGet]
        public IActionResult GetUsersData(string q = "", string status = "All")
        {
            var query = _context.UserSignup.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim().ToLower();
                query = query.Where(u =>
                    u.Name.ToLower().Contains(q) ||
                    u.Email.ToLower().Contains(q) ||
                    (u.Phone != null && u.Phone.Contains(q)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(u => u.Status == status);
            }

            var list = query
                .Select(u => new UserListModel
                {
                    UserId = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Phone = u.Phone,
                    OrdersCount = _context.OrderTable.Count(o => o.UserId == u.Id),
                    Status = u.Status,
                    RegisteredAt = u.CreatedAt
                })
                .ToList();

            return Json(list);
        }

        // ================= BLOCK USER =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BlockUser(int userId, bool block)
        {
            var user = _context.UserSignups.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return Json(new { success = false });

            user.Status = block ? "Blocked" : "Active";
            _context.SaveChanges();

            return Json(new { success = true });
        }

        // ================= USER ORDERS =================
        [HttpGet]
        public IActionResult GetUserOrdersData(int userId, DateTime? from, DateTime? to)
        {
            DateTime end = to?.Date ?? DateTime.Today;
            DateTime start = from?.Date ?? end.AddDays(-6);

            var orders = _context.OrderTables
                .Where(o => o.UserId == userId &&
                            o.CreatedAt >= start &&
                            o.CreatedAt <= end)
                .Select(o => new
                {
                    OrderId = o.OrderId,
                    TotalItems = o.TotalItems,
                    TotalCalories = o.TotalCalories,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt
                })
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            int totalCalories = orders.Sum(o => o.TotalCalories ?? 0);
            double days = Math.Max(1, (end - start).TotalDays + 1);
            double dailyAvg = Math.Round(totalCalories / days, 2);

            return Json(new
            {
                userId,
                from = start,
                to = end,
                orders,
                totalCalories,
                dailyAverage = dailyAvg
            });
        }

        // ================= CALORIE ANALYTICS =================
        [HttpGet]
        public IActionResult GetCalorieAnalyticsData(int userId, DateTime? from, DateTime? to)
        {
            DateTime end = to?.Date ?? DateTime.Today;
            DateTime start = from?.Date ?? end.AddDays(-13);

            var entries = _context.DailyCalorieEntry
                .Where(e => e.UserId == userId &&
                            e.Date >= start &&
                            e.Date <= end)
                .ToList();

            var grouped = entries
                .GroupBy(e => e.Date.Date)
                .Select(g => new CalorieTrendPoint
                {
                    Label = g.Key.ToString("yyyy-MM-dd"),
                    Calories = g.Sum(x => x.Calories)
                })
                .OrderBy(g => g.Label)
                .ToArray();

            double avg = grouped.Any() ? grouped.Average(g => g.Calories) : 0;
            int peak = grouped.Any() ? grouped.Max(g => g.Calories) : 0;

            return Json(new UserCalorieAnalyticsModel
            {
                UserId = userId,
                From = start,
                To = end,
                AverageDaily = Math.Round(avg, 2),
                PeakDaily = peak,
                Trend = grouped
            });
        }
    }
}