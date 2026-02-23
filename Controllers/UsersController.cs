using Microsoft.AspNetCore.Mvc;
using NUTRIBITE.Models.Users;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace NUTRIBITE.Controllers
{
    // Make this partial so it merges with other partial UsersController files
    public partial class UsersController : Controller
    {
        // GET: /Users
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Users/GetUsersData
        [HttpGet]
        public IActionResult GetUsersData(string q = "", string status = "All")
        {
            // Sample data - replace with DB query returning UserListModel
            var list = new List<UserListModel>
            {
                new UserListModel { UserId = 1001, Name = "Asha Verma", Email = "asha@example.com", Phone = "9123456780", OrdersCount = 12, Status = "Active", RegisteredAt = DateTime.Today.AddMonths(-4) },
                new UserListModel { UserId = 1002, Name = "Ravi Kumar", Email = "ravi@example.com", Phone = "9988776655", OrdersCount = 3, Status = "Blocked", RegisteredAt = DateTime.Today.AddMonths(-1) },
                new UserListModel { UserId = 1003, Name = "Sonal Mehta", Email = "sonal@example.com", Phone = "9876543210", OrdersCount = 27, Status = "Active", RegisteredAt = DateTime.Today.AddYears(-1) },
                new UserListModel { UserId = 1004, Name = "Deepak Jain", Email = "deepak@example.com", Phone = "9012345678", OrdersCount = 0, Status = "Inactive", RegisteredAt = DateTime.Today.AddDays(-10) }
            };

            // simple filtering
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim().ToLowerInvariant();
                list = list.FindAll(u =>
                    u.Name.ToLowerInvariant().Contains(q) ||
                    u.Email.ToLowerInvariant().Contains(q) ||
                    u.Phone.ToLowerInvariant().Contains(q));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                list = list.FindAll(u => string.Equals(u.Status, status, StringComparison.OrdinalIgnoreCase));
            }

            return Json(list);
        }

        // POST: /Users/BlockUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BlockUser(int userId, bool block)
        {
            // In real app: update DB to block/unblock user
            // Here return success so client can update UI
            return Json(new { success = true, userId = userId, blocked = block });
        }

        // GET: /Users/Profile?userId=1001
        [HttpGet]
        public IActionResult Profile(int userId)
        {
            ViewBag.UserId = userId;
            return View();
        }

        // JSON: /Users/GetUserProfileData?userId=1001
        [HttpGet]
        public IActionResult GetUserProfileData(int userId)
        {
            // TODO: Replace with DB query by userId. Sample/mock data returned below.
            var rnd = new Random(userId);
            var trend = Enumerable.Range(0, 7)
                .Select(i => new TrendPoint
                {
                    Label = DateTime.Today.AddDays(-6 + i).ToString("ddd"),
                    Calories = 1400 + rnd.Next(0, 800)
                }).ToArray();

            var model = new UserHealthProfileModel
            {
                UserId = userId,
                Name = $"User {userId}",
                Email = $"user{userId}@example.com",
                Phone = "9" + rnd.Next(100000000, 999999999).ToString().Substring(0,9),
                Status = (rnd.NextDouble() > 0.85) ? "At Risk" : "Active",
                RegisteredAt = DateTime.Today.AddMonths(-rnd.Next(0, 24)),
                DietType = (new[] { "Veg", "Vegan", "Non-Veg", "Jain" })[rnd.Next(4)],
                CalorieGoal = 2000,
                TodayCalories = trend.Last().Calories,
                WeeklyCaloriesAverage = (int)trend.Average(t => t.Calories),
                OrdersCount = rnd.Next(0, 120),
                WeeklyTrend = trend,
                AdminNote = "No recent issues."
            };

            return Json(model);
        }

        // GET: /Users/UserOrders?userId=1001
        [HttpGet]
        public IActionResult UserOrders(int userId)
        {
            ViewBag.UserId = userId;
            return View();
        }

        // JSON: /Users/GetUserOrdersData?userId=1001&from=2026-01-01&to=2026-01-07
        [HttpGet]
        public IActionResult GetUserOrdersData(int userId, DateTime? from, DateTime? to)
        {
            DateTime end = to?.Date ?? DateTime.Today;
            DateTime start = from?.Date ?? end.AddDays(-6); // default last 7 days
            if (start > end) (start, end) = (end, start);

            // Mocked sample orders - replace with DB query - include calories per meal
            var rnd = new Random(userId);
            var orders = new List<OrderItem>();
            int orderId = 5000;
            for (DateTime d = start; d <= end; d = d.AddDays(1))
            {
                int perDay = 1 + rnd.Next(3); // 1..3 orders per day
                for (int i = 0; i < perDay; i++)
                {
                    var meal = new[] { "Special Thali", "Salad Bowl", "Protein Meal", "Classic Thali", "Breakfast Combo" }[rnd.Next(5)];
                    var cal = rnd.Next(250, 900);
                    var status = (rnd.NextDouble() > 0.95) ? "Cancelled" : "Picked";
                    orders.Add(new OrderItem
                    {
                        OrderId = orderId++,
                        MealName = meal,
                        OrderDate = d.AddHours(8 + rnd.Next(8)).AddMinutes(rnd.Next(60)),
                        Calories = cal,
                        Status = status
                    });
                }
            }

            int totalCalories = orders.Sum(o => o.Calories);
            double days = Math.Max(1, (end - start).TotalDays + 1);
            double dailyAvg = Math.Round(totalCalories / days, 2);

            var result = new
            {
                userId = userId,
                userName = $"User {userId}",
                from = start,
                to = end,
                orders = orders.OrderByDescending(o => o.OrderDate).ToArray(),
                totalCalories = totalCalories,
                dailyAverage = dailyAvg
            };

            return Json(result);
        }

        // GET: /Users/CalorieAnalytics
        // Session-protected, redirects to Auth/Login when not authenticated.
        [HttpGet]
        public IActionResult CalorieAnalytics()
        {
            var uid = HttpContext.Session.GetInt32("UserId");

            if (!uid.HasValue)
                return RedirectToAction("Login", "Auth");

            return View();
        }

        [HttpGet]
        public IActionResult GetCalorieAnalyticsData(int userId = 0, DateTime? from = null, DateTime? to = null)
        {
            // Mocked sample data. Replace with DB queries and real aggregations.
            DateTime end = to?.Date ?? DateTime.Today;
            DateTime start = from?.Date ?? end.AddDays(-13);
            if (start > end) (start, end) = (end, start);

            var rnd = new Random(userId == 0 ? 42 : userId);
            var days = (end - start).Days + 1;
            var trend = new System.Collections.Generic.List<NUTRIBITE.Models.Users.CalorieTrendPoint>();
            int peak = 0;
            int total = 0;

            for (int i = 0; i < days; i++)
            {
                var d = start.AddDays(i);
                int cal = 1400 + rnd.Next(0, 900); // sample calories
                trend.Add(new NUTRIBITE.Models.Users.CalorieTrendPoint { Label = d.ToString("MM-dd"), Calories = cal });
                total += cal;
                if (cal > peak) peak = cal;
            }

            double avg = days > 0 ? Math.Round((double)total / days, 2) : 0.0;
            int recommended = 2000; // this could be user-specific from DB
            double comparison = recommended > 0 ? Math.Round((avg / recommended) * 100.0, 1) : 0.0;

            var alerts = new[]
            {
                new NUTRIBITE.Models.Users.AnalyticsAlert { Level = "info", Message = "This is informational only: data is sample." },
                avg > recommended ? new NUTRIBITE.Models.Users.AnalyticsAlert { Level = "warning", Message = "Average intake is above recommended level." } 
                                  : new NUTRIBITE.Models.Users.AnalyticsAlert { Level = "info", Message = "Average intake within recommended range." }
            };

            var model = new NUTRIBITE.Models.Users.UserCalorieAnalyticsModel
            {
                UserId = userId,
                UserName = userId == 0 ? "All Users" : $"User {userId}",
                From = start,
                To = end,
                AverageDaily = avg,
                PeakDaily = peak,
                RecommendedDaily = recommended,
                Trend = trend.ToArray(),
                ComparisonPercent = comparison,
                Alerts = alerts
            };

            return Json(model);
        }

        // Add these methods inside the existing UsersController class.

        // GET: /Users/CouponMisuse
        [HttpGet]
        public IActionResult CouponMisuse()
        {
            return View();
        }

        // GET: /Users/GetCouponMisuseData
        [HttpGet]
        public IActionResult GetCouponMisuseData(string coupon = "", DateTime? from = null, DateTime? to = null, string status = "All")
        {
            // Sample/mock data. Replace with DB queries that aggregate coupon usage by user.
            DateTime end = to?.Date ?? DateTime.Today;
            DateTime start = from?.Date ?? end.AddDays(-13);
            if (start > end) (start, end) = (end, start);

            var rnd = new Random(42);
            var users = new[]
            {
                new { Id = 1001, Name = "Asha Verma" },
                new { Id = 1002, Name = "Ravi Kumar" },
                new { Id = 1003, Name = "Sonal Mehta" },
                new { Id = 1004, Name = "Deepak Jain" },
                new { Id = 1005, Name = "Pooja Sharma" }
            };

            var codes = new[] { "WELCOME10", "FIRST50", "FREEMEAL", "SUMMER21" };

            var rows = new List<CouponRow>();
            foreach (var u in users)
            {
                var code = codes[rnd.Next(codes.Length)];
                if (!string.IsNullOrEmpty(coupon) && !code.Equals(coupon, StringComparison.OrdinalIgnoreCase))
                    continue;

                int uses = rnd.Next(1, 12);
                int cancels = rnd.Next(0, Math.Min(4, uses));
                var last = end.AddDays(-rnd.Next(0, (end - start).Days + 1));
                // simple misuse scoring: more uses + cancels raise score
                double score = Math.Min(100, uses * 6 + cancels * 12 + rnd.NextDouble() * 10);

                var st = score > 60 ? "Suspicious" : (score > 40 ? "Review" : "Normal");
                if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) && !st.Equals(status, StringComparison.OrdinalIgnoreCase))
                    continue;

                rows.Add(new CouponRow
                {
                    UserId = u.Id,
                    UserName = u.Name,
                    CouponCode = code,
                    Uses = uses,
                    Cancels = cancels,
                    LastUsed = last,
                    MisuseScore = Math.Round(score, 1),
                    Status = st
                });
            }

            var summary = new MisuseSummary
            {
                TotalUsersObserved = rows.Count,
                SuspiciousCount = rows.Count(r => r.MisuseScore > 60),
                AvgMisuseScore = rows.Any() ? Math.Round(rows.Average(r => r.MisuseScore), 1) : 0
            };

            var model = new CouponMisuseModel
            {
                Summary = summary,
                Rows = rows.OrderByDescending(r => r.MisuseScore).ToArray()
            };

            return Json(model);
        }

        // POST: /Users/WarnUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult WarnUser(int userId, string message)
        {
            // Implement persistent warning (DB / notification) in real app.
            // For now return success for UI feedback.
            return Json(new { success = true, userId, message });
        }

        // POST: /Users/RestrictCoupon
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RestrictCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
                return Json(new { success = false, message = "Coupon required" });

            // Implement coupon restriction in DB/payment rules in real app.
            return Json(new { success = true, couponCode });
        }
    }
}