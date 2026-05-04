using Microsoft.AspNetCore.Mvc;
using global::NUTRIBITE.Filters;
using global::NUTRIBITE.Models;
using global::NUTRIBITE.Models.Reports;
using global::NUTRIBITE.Services;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace NUTRIBITE.Controllers
{
    [AdminAuthorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public ReportsController(ApplicationDbContext context, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // Main entry for Reports & Analytics
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpGet]
        public IActionResult SystemLogs()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSystemLogs(string severity = "All", DateTime? from = null, DateTime? to = null, string? q = null, int page = 1)
        {
            try
            {
                DateTime end = (to ?? DateTime.Now).Date.AddDays(1).AddTicks(-1);
                DateTime start = from?.Date ?? DateTime.Now.Date.AddDays(-7);
                
                var query = _context.ActivityLogs.AsQueryable();
                
                query = query.Where(l => l.Timestamp >= start && l.Timestamp <= end);

                if (!string.IsNullOrEmpty(q))
                {
                    query = query.Where(a => 
                        (a.Action != null && a.Action.Contains(q)) || 
                        (a.Details != null && a.Details.Contains(q)) || 
                        (a.AdminEmail != null && a.AdminEmail.Contains(q)));
                }

                var totalCount = await query.CountAsync();
                var logs = await query.OrderByDescending(a => a.Timestamp)
                                     .Skip((page - 1) * 50)
                                     .Take(50)
                                     .ToListAsync();

                var items = logs.Select(a => {
                    var level = "Info";
                    if (!string.IsNullOrEmpty(a.Action)) {
                        if (a.Action.Contains("Error", StringComparison.OrdinalIgnoreCase) || a.Action.Contains("Fail", StringComparison.OrdinalIgnoreCase)) level = "Error";
                        else if (a.Action.Contains("Warn", StringComparison.OrdinalIgnoreCase) || a.Action.Contains("Delete", StringComparison.OrdinalIgnoreCase)) level = "Warning";
                    }
                    if (!string.IsNullOrEmpty(a.Details) && level == "Info") {
                         if (a.Details.Contains("Error", StringComparison.OrdinalIgnoreCase) || a.Details.Contains("Fail", StringComparison.OrdinalIgnoreCase)) level = "Error";
                    }

                    return new {
                        timestamp = a.Timestamp,
                        level = level,
                        source = a.Action ?? "System",
                        message = !string.IsNullOrEmpty(a.Details) ? a.Details : a.Action,
                        user = a.AdminEmail ?? "System",
                        details = (a.Details ?? "") + (!string.IsNullOrEmpty(a.IpAddress) ? "\nIP: " + a.IpAddress : "")
                    };
                });

                if (!string.IsNullOrEmpty(severity) && severity != "All" && severity != "All Severities")
                {
                    items = items.Where(l => string.Equals(l.level, severity, StringComparison.OrdinalIgnoreCase));
                }

                return Json(new { success = true, totalCount = totalCount, items = items.ToList() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Focused report pages - views live in Views/Reports
        [HttpGet]
        public IActionResult OrderReports()
        {
            return View(new OrderReportModel());
        }

        [HttpGet]
        public IActionResult SalesAnalytics()
        {
            // return view; the view will request JSON data to render cards/chart/table
            return View(new NUTRIBITE.Models.Reports.SalesReportModel());
        }

        [HttpGet]
        public IActionResult VendorPerformance()
        {
            return View(new VendorPerformanceModel());
        }

        [HttpGet]
        public IActionResult LocationAnalytics()
        {
            // returns the view located at Views/Reports/LocationAnalytics.cshtml
            return View();
        }

        [HttpGet]
        public IActionResult PaymentReports()
        {
            // View will request JSON data; provide an (empty) model for initial load
            return View(new NUTRIBITE.Models.Reports.PaymentReportModel());
        }

        [HttpGet]
        public IActionResult GetPaymentReportsData(string from, string to, string status, string period, string? username = null, string? gatewayRef = null, DateTime? refDate = null)
        {
            try
            {
                DateTime end = string.IsNullOrEmpty(to) ? DateTime.Today.AddDays(1) : (DateTime.TryParse(to, out var dTo) ? dTo.AddDays(1) : DateTime.Today.AddDays(1));
                DateTime start = string.IsNullOrEmpty(from) ? new DateTime(2020, 1, 1) : (DateTime.TryParse(from, out var dFrom) ? dFrom : new DateTime(2020, 1, 1));
                
                var query = _context.OrderTables.Include(o => o.Payments).AsQueryable();
                
                // Exclude Cash on Delivery since this is a digital payment report
                query = query.Where(o => o.CreatedAt >= start && o.CreatedAt < end && o.PaymentStatus != null && !o.Payments.Any(p => p.PaymentMode == "Cash on Delivery"));
            
                if (!string.IsNullOrEmpty(status) && status != "All")
                {
                    if (status == "Passed") {
                        query = query.Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Success" || o.PaymentStatus == "Paid");
                    } else {
                        query = query.Where(o => o.PaymentStatus == status);
                    }
                }

                if (!string.IsNullOrEmpty(username))
                {
                    query = query.Where(o => o.CustomerName != null && o.CustomerName.Contains(username));
                }

                if (!string.IsNullOrEmpty(gatewayRef))
                {
                    query = query.Where(o => o.Payments.Any(p => p.TransactionId != null && p.TransactionId.Contains(gatewayRef)));
                }

                var ordersList = query.OrderByDescending(o => o.CreatedAt).ToList();
                
                // Calculate aggregations
                var totalSuccess = ordersList.Count(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Success");
                var totalFailed = ordersList.Count(o => o.PaymentStatus == "Failed" || o.PaymentStatus == "Error");
                var totalRefunded = ordersList.Count(o => o.PaymentStatus == "Refunded");
                var totalCancelled = ordersList.Count(o => o.Status == "Cancelled");

                // Format for Frontend Table
                var paymentsFormatted = ordersList.Select(o => new
                {
                    PaymentId = o.OrderId,
                    OrderId = o.OrderId,
                    PaymentDate = o.CreatedAt,
                    CustomerName = o.CustomerName ?? "System",
                    Method = o.Payments.FirstOrDefault()?.PaymentMode ?? "Card/UPI",
                    Amount = o.TotalAmount,
                    Status = o.PaymentStatus ?? "Pending",
                    GatewayRef = o.Payments.FirstOrDefault()?.TransactionId ?? "TID-SYS-" + o.OrderId,
                    Notes = o.Payments.FirstOrDefault()?.PaymentMode == "Cash on Delivery" ? "COD" : "Online Gateway"
                }).ToList();

                // Professional Trend Building
                var labels = new List<string>();
                var successAmount = new List<decimal>();
                var cancelledCount = new List<int>();
                var refundedCount = new List<int>();

                if (period == "yearly")
                {
                    var groupedOrders = ordersList.GroupBy(o => o.CreatedAt?.Year ?? DateTime.Now.Year);
                    
                    DateTime trendRef = refDate?.Date ?? DateTime.Today;
                    DateTime tEnd = string.IsNullOrEmpty(to) ? trendRef : end.AddDays(-1);
                    DateTime tStart = string.IsNullOrEmpty(from) ? tEnd : start;
                    if (tStart > tEnd) { var t = tStart; tStart = tEnd; tEnd = t; }

                    int startYear = tStart.Year;
                    int endYear = tEnd.Year;
                    
                    for (int y = startYear; y <= endYear; y++)
                    {
                        labels.Add(y.ToString());
                        var oGrp = groupedOrders.FirstOrDefault(g => g.Key == y);
                        
                        successAmount.Add(oGrp?.Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Success").Sum(x => x.TotalAmount) ?? 0m);
                        cancelledCount.Add(oGrp?.Count(o => o.Status == "Cancelled") ?? 0);
                        refundedCount.Add(oGrp?.Count(o => o.PaymentStatus == "Refunded") ?? 0);
                    }
                }
                else if (period == "monthly")
                {
                    var groupedOrders = ordersList.GroupBy(o => new { Y = o.CreatedAt?.Year ?? DateTime.Now.Year, M = o.CreatedAt?.Month ?? DateTime.Now.Month });
                    
                    DateTime trendRef = refDate?.Date ?? DateTime.Today;
                    DateTime tEnd = string.IsNullOrEmpty(to) ? trendRef : end.AddDays(-1);
                    DateTime tStart = string.IsNullOrEmpty(from) ? tEnd.AddMonths(-11).AddDays(-tEnd.Day + 1) : start;
                    if (tStart > tEnd) { var t = tStart; tStart = tEnd; tEnd = t; }

                    var currentMonth = new DateTime(tStart.Year, tStart.Month, 1);
                    var endMonth = new DateTime(tEnd.Year, tEnd.Month, 1);
                    
                    if (currentMonth > endMonth) endMonth = currentMonth;
                    
                    while (currentMonth <= endMonth)
                    {
                        labels.Add($"{currentMonth:MMM yyyy}");
                        var mKeyY = currentMonth.Year;
                        var mKeyM = currentMonth.Month;
                        
                        var oGrp = groupedOrders.FirstOrDefault(g => g.Key.Y == mKeyY && g.Key.M == mKeyM);
                        
                        successAmount.Add(oGrp?.Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Success").Sum(x => x.TotalAmount) ?? 0m);
                        cancelledCount.Add(oGrp?.Count(o => o.Status == "Cancelled") ?? 0);
                        refundedCount.Add(oGrp?.Count(o => o.PaymentStatus == "Refunded") ?? 0);
                        
                        currentMonth = currentMonth.AddMonths(1);
                    }
                }
                else
                {
                    var groupedOrders = ordersList.GroupBy(o => o.CreatedAt?.Date ?? DateTime.Today);
                    
                    DateTime trendRef = refDate?.Date ?? DateTime.Today;
                    DateTime tEnd = string.IsNullOrEmpty(to) ? trendRef : end.AddDays(-1);
                    DateTime tStart = string.IsNullOrEmpty(from) ? (period == "weekly" ? tEnd.AddDays(-84) : tEnd.AddDays(-13)) : start;
                    if (tStart > tEnd) { var t = tStart; tStart = tEnd; tEnd = t; }

                    var currentDay = tStart.Date;
                    var endDay = tEnd.Date;
                    
                    while (currentDay <= endDay)
                    {
                        labels.Add(currentDay.ToString("dd MMM yyyy"));
                        var oGrp = groupedOrders.FirstOrDefault(g => g.Key == currentDay);
                        
                        successAmount.Add(oGrp?.Where(o => o.PaymentStatus == "Completed" || o.PaymentStatus == "Paid" || o.PaymentStatus == "Success").Sum(x => x.TotalAmount) ?? 0m);
                        cancelledCount.Add(oGrp?.Count(o => o.Status == "Cancelled") ?? 0);
                        refundedCount.Add(oGrp?.Count(o => o.PaymentStatus == "Refunded") ?? 0);
                        
                        currentDay = currentDay.AddDays(1);
                    }
                }

                return Json(new {
                    success = true,
                    totalSuccess = totalSuccess,
                    totalFailed = totalFailed,
                    totalRefunded = totalRefunded,
                    totalCancelled = totalCancelled,
                    payments = paymentsFormatted,
                    trend = new {
                        labels = labels,
                        successAmount = successAmount,
                        cancelledCount = cancelledCount,
                        refundedCount = refundedCount
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetOrderReportsData(DateTime? from, DateTime? to, string status, string period = "monthly")
        {
            try
            {
                DateTime end = to?.Date ?? DateTime.Today;
                DateTime start = from?.Date ?? (period == "yearly" ? end : (period == "monthly" ? end.AddMonths(-11).AddDays(-end.Day + 1) : DateTime.Today.AddDays(-13)));
                if (start > end) (start, end) = (end, start);
                DateTime endExclusive = end.AddDays(1);

                var query = _context.OrderTables
                    .Include(o => o.OrderItems)
                    .Include(o => o.Payments)
                    .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive);

                if (!string.IsNullOrWhiteSpace(status) && status != "All")
                {
                    query = query.Where(o => o.Status == status);
                }

                var orders = query
                    .AsEnumerable()
                    .Select(o => (dynamic)new
                    {
                        OrderId = o.OrderId,
                        OrderDate = o.CreatedAt,
                        CustomerName = o.CustomerName ?? "Customer #" + o.OrderId,
                        ItemsCount = o.TotalItems ?? o.OrderItems?.Count ?? 1,
                        PickupSlot = o.PickupSlot ?? "N/A",
                        Amount = (o.Payments != null && o.Payments.Any()) ? o.Payments.Sum(p => p.Amount ?? 0m) : o.TotalAmount,
                        TotalCalories = o.TotalCalories ?? 0,
                        PaymentStatus = o.PaymentStatus ?? "Pending",
                        Status = o.Status ?? "New"
                    })
                    .OrderByDescending(x => x.OrderDate)
                    .ToList();


                var total = orders.Count;
                var completed = orders.Count(o => string.Equals((string)o.Status, "Delivered", StringComparison.OrdinalIgnoreCase) || string.Equals((string)o.Status, "Completed", StringComparison.OrdinalIgnoreCase));
                var pending = orders.Count(o => new[] { "New", "Accepted", "Ready for Delivery", "Ready for Pickup" }.Contains(((string)o.Status ?? ""), StringComparer.OrdinalIgnoreCase));
                var cancelled = orders.Count(o => string.Equals((string)o.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));

                var summary = new[]
                {
                    new SummaryCard { Title = "Total Orders", Value = total.ToString(), SubText = $"{start:yyyy-MM-dd} → {end:yyyy-MM-dd}" },
                    new SummaryCard { Title = "Completed", Value = completed.ToString(), SubText = "Delivered / Completed" },
                    new SummaryCard { Title = "Pending", Value = pending.ToString(), SubText = "New / Accepted / Ready" },
                    new SummaryCard { Title = "Cancelled", Value = cancelled.ToString(), SubText = "Cancelled orders" }
                };

                var trendLabels = new List<string>();
                var revenueTrend = new List<decimal>();
                var orderTrend = new List<int>();

                if (period == "yearly")
                {
                    var yearlyTrend = _context.OrderTables
                        .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
                        .GroupBy(o => o.CreatedAt.Value.Year)
                        .Select(g => new { Label = g.Key.ToString(), Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                        .OrderBy(x => x.Label)
                        .ToList();
                    
                    if (!yearlyTrend.Any())
                    {
                        trendLabels.Add(DateTime.Now.Year.ToString());
                        revenueTrend.Add(0m);
                        orderTrend.Add(0);
                    }
                    else {
                        foreach (var x in yearlyTrend) {
                            trendLabels.Add(x.Label);
                            revenueTrend.Add(x.Revenue);
                            orderTrend.Add(x.Count);
                        }
                    }
                }
                else if (period == "monthly")
                {
                    var monthlyTrend = _context.OrderTables
                        .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
                        .GroupBy(o => new { o.CreatedAt.Value.Year, o.CreatedAt.Value.Month })
                        .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                        .ToList();

                    int monthsCount = ((end.Year - start.Year) * 12 + end.Month - start.Month);
                    if (monthsCount < 0) monthsCount = 0;

                    for (int i = 0; i <= monthsCount; i++)
                    {
                        var d = start.AddMonths(i);
                        var match = monthlyTrend.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
                        trendLabels.Add(d.ToString("MMM yyyy"));
                        revenueTrend.Add(match?.Revenue ?? 0m);
                        orderTrend.Add(match?.Count ?? 0);
                    }
                }
                else if (period == "weekly")
                {
                    var ordersData = _context.OrderTables
                        .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
                        .Select(o => new { o.CreatedAt, o.TotalAmount })
                        .ToList();

                    var startDateAdjusted = start.AddDays(-(int)start.DayOfWeek);
                    var endDateAdjusted = end.AddDays(-(int)end.DayOfWeek);

                    for (var d = startDateAdjusted; d <= endDateAdjusted; d = d.AddDays(7))
                    {
                        var weekEnd = d.AddDays(7);
                        var match = ordersData.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Date >= d && o.CreatedAt.Value.Date < weekEnd).ToList();
                        trendLabels.Add(d.ToString("MMM dd"));
                        revenueTrend.Add(match.Sum(x => x.TotalAmount));
                        orderTrend.Add(match.Count);
                    }
                }
                else // Daily
                {
                    var dailyTrend = _context.OrderTables
                        .Where(o => o.CreatedAt >= start && o.CreatedAt < endExclusive)
                        .GroupBy(o => o.CreatedAt.Value.Date)
                        .Select(g => new { Date = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
                        .ToDictionary(x => x.Date, x => new { x.Count, x.Revenue });

                    for (int i = 0; i <= (end - start).Days; i++)
                    {
                        var d = start.AddDays(i);
                        trendLabels.Add(d.ToString("MM-dd"));
                        if (dailyTrend.TryGetValue(d.Date, out var val)) {
                            revenueTrend.Add(val.Revenue);
                            orderTrend.Add(val.Count);
                        } else {
                            revenueTrend.Add(0m);
                            orderTrend.Add(0);
                        }
                    }
                }

                return Json(new { 
                    summary, 
                    orders, 
                    trend = new {
                        labels = trendLabels,
                        revenue = revenueTrend,
                        orders = orderTrend
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetSalesAnalyticsData(DateTime? from, DateTime? to, string period = "monthly", DateTime? refDate = null)
        {
            DateTime reference = refDate?.Date ?? DateTime.Today;
            DateTime end = to?.Date ?? reference;
            DateTime start = from?.Date ?? reference;
            if (start > end) (start, end) = (end, start);
            
            // Calculate summary bounds
            DateTime summaryStart = start;
            DateTime summaryEnd = end.AddDays(1);
            if (!from.HasValue && !to.HasValue) 
            {
                string p = period?.ToLowerInvariant();
                if (p == "daily" || p == "today") { summaryStart = reference.Date; summaryEnd = summaryStart.AddDays(1); }
                else if (p == "weekly") { summaryStart = reference.Date.AddDays(-(int)reference.DayOfWeek); summaryEnd = summaryStart.AddDays(7); }
                else if (p == "monthly") { summaryStart = new DateTime(reference.Year, reference.Month, 1); summaryEnd = summaryStart.AddMonths(1); }
                else if (p == "yearly") { summaryStart = new DateTime(reference.Year, 1, 1); summaryEnd = summaryStart.AddYears(1); }
            }

            var paymentsInRange = _context.Payments
                .Where(p => p.CreatedAt >= summaryStart && p.CreatedAt < summaryEnd)
                .ToList();

            var allOrders = _context.OrderTables
                .Where(o => o.CreatedAt >= summaryStart && o.CreatedAt < summaryEnd)
                .ToList();

            var completedOrders = allOrders.Where(o => o.Status != "Cancelled").ToList();

            decimal totalRevenue = paymentsInRange.Sum(p => p.Amount ?? 0m);
            if (totalRevenue == 0) totalRevenue = completedOrders.Sum(o => o.TotalAmount);
            
            int totalOrders = completedOrders.Count;

            decimal avgOrder = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m;
            decimal profit = Math.Round(totalRevenue * 0.10m, 2); 
            decimal totalLoss = allOrders.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount);

            var breakdown = new List<NUTRIBITE.Models.Reports.BreakdownRow>();
            var trend = new List<NUTRIBITE.Models.Reports.TrendPoint>();

            DateTime trendEnd = string.IsNullOrEmpty(to?.ToString()) ? reference : end;
            DateTime trendStart = string.IsNullOrEmpty(from?.ToString()) ? 
                (period == "yearly" ? trendEnd : (period == "monthly" ? trendEnd.AddMonths(-11).AddDays(-trendEnd.Day + 1) : (period == "weekly" ? trendEnd.AddDays(-84) : trendEnd.AddDays(-13)))) 
                : start;
            if (trendStart > trendEnd) { var t = trendStart; trendStart = trendEnd; trendEnd = t; }

            var allTrendOrders = _context.OrderTables
                .Where(o => o.CreatedAt >= trendStart && o.CreatedAt < trendEnd.AddDays(1))
                .ToList();

            if (period?.ToLowerInvariant() == "yearly")
            {
                var yearlyGroups = allTrendOrders.GroupBy(o => o.CreatedAt.Value.Year).ToList();
                for (int y = trendStart.Year; y <= trendEnd.Year; y++)
                {
                    var g = yearlyGroups.FirstOrDefault(gx => gx.Key == y);
                    var rev = g?.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    var ord = g?.Count(o => o.Status != "Cancelled") ?? 0;
                    var loss = g?.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    
                    breakdown.Add(new NUTRIBITE.Models.Reports.BreakdownRow { PeriodLabel = y.ToString(), Orders = ord, Revenue = rev, Loss = loss, AvgOrderValue = ord > 0 ? Math.Round(rev / ord, 2) : 0m, Profit = Math.Round(rev * 0.10m, 2) });
                    trend.Add(new NUTRIBITE.Models.Reports.TrendPoint { Label = y.ToString(), Revenue = rev, Orders = ord });
                }
            }
            else if (period?.ToLowerInvariant() == "monthly")
            {
                var monthlyGroups = allTrendOrders.GroupBy(o => new { o.CreatedAt.Value.Year, o.CreatedAt.Value.Month }).ToList();
                var currentMonth = new DateTime(trendStart.Year, trendStart.Month, 1);
                var endMonth = new DateTime(trendEnd.Year, trendEnd.Month, 1);
                
                while (currentMonth <= endMonth)
                {
                    var g = monthlyGroups.FirstOrDefault(gx => gx.Key.Year == currentMonth.Year && gx.Key.Month == currentMonth.Month);
                    var label = $"{currentMonth:MMM yyyy}";
                    var rev = g?.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    var ord = g?.Count(o => o.Status != "Cancelled") ?? 0;
                    var loss = g?.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    
                    breakdown.Add(new NUTRIBITE.Models.Reports.BreakdownRow { PeriodLabel = label, Orders = ord, Revenue = rev, Loss = loss, AvgOrderValue = ord > 0 ? Math.Round(rev / ord, 2) : 0m, Profit = Math.Round(rev * 0.10m, 2) });
                    trend.Add(new NUTRIBITE.Models.Reports.TrendPoint { Label = label, Revenue = rev, Orders = ord });
                    currentMonth = currentMonth.AddMonths(1);
                }
            }
            else if (period?.ToLowerInvariant() == "weekly")
            {
                var weeklyGroups = allTrendOrders.GroupBy(o => o.CreatedAt.Value.Date).ToList();
                var currentDay = trendStart.Date;
                var endDay = trendEnd.Date;
                
                while (currentDay <= endDay)
                {
                    var g = weeklyGroups.FirstOrDefault(gx => gx.Key == currentDay);
                    var label = currentDay.ToString("dd MMM yyyy");
                    var rev = g?.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    var ord = g?.Count(o => o.Status != "Cancelled") ?? 0;
                    var loss = g?.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    
                    breakdown.Add(new NUTRIBITE.Models.Reports.BreakdownRow { PeriodLabel = label, Orders = ord, Revenue = rev, Loss = loss, AvgOrderValue = ord > 0 ? Math.Round(rev / ord, 2) : 0m, Profit = Math.Round(rev * 0.10m, 2) });
                    trend.Add(new NUTRIBITE.Models.Reports.TrendPoint { Label = label, Revenue = rev, Orders = ord });
                    currentDay = currentDay.AddDays(1);
                }
            }
            else // daily
            {
                var dailyGroups = allTrendOrders.GroupBy(o => o.CreatedAt.Value.Hour).ToList();
                int currentHour = summaryEnd.AddDays(-1).Date == DateTime.Today ? DateTime.Now.Hour + 1 : 24;
                
                for (int h = 0; h < currentHour; h++)
                {
                    var g = dailyGroups.FirstOrDefault(gx => gx.Key == h);
                    var label = $"{h:00}:00 - {h + 1:00}:00";
                    var rev = g?.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    var ord = g?.Count(o => o.Status != "Cancelled") ?? 0;
                    var loss = g?.Where(o => o.Status == "Cancelled").Sum(o => o.TotalAmount) ?? 0m;
                    
                    breakdown.Add(new NUTRIBITE.Models.Reports.BreakdownRow { PeriodLabel = label, Orders = ord, Revenue = rev, Loss = loss, AvgOrderValue = ord > 0 ? Math.Round(rev / ord, 2) : 0m, Profit = Math.Round(rev * 0.10m, 2) });
                    trend.Add(new NUTRIBITE.Models.Reports.TrendPoint { Label = label, Revenue = rev, Orders = ord });
                }
            }

            var model = new NUTRIBITE.Models.Reports.SalesReportModel
            {
                TotalRevenue = Math.Round(totalRevenue, 2),
                AverageOrderValue = Math.Round(avgOrder, 2),
                Profit = profit,
                TotalLoss = totalLoss,
                Trend = trend.ToArray(),
                Breakdown = breakdown.ToArray()
            };

            return Json(model);
        }

        [HttpGet]
        public IActionResult GetVendorPerformanceData(DateTime? from, DateTime? to, int top = 10, string period = "monthly")
        {
            DateTime end = to?.Date ?? DateTime.Today;
            DateTime start = from?.Date ?? (period == "yearly" ? end : (period == "monthly" ? end.AddMonths(-11).AddDays(-end.Day + 1) : (period == "weekly" ? end.AddDays(-84) : end.AddDays(-29))));
            if (start > end) (start, end) = (end, start);
            DateTime endExclusive = end.AddDays(1);

            var vendors = _context.VendorSignups.ToList();
            var vendorRows = new List<Models.Reports.VendorRow>();
            
            foreach (var v in vendors)
            {
                var orderIds = _context.OrderItems
                    .Include(oi => oi.Food)
                    .Where(oi => (oi.Food != null && oi.Food.VendorId == v.VendorId) || 
                                 (_context.BulkItems.Any(b => b.Id == oi.BulkItemId && b.VendorId == v.VendorId)))
                    .Select(oi => oi.OrderId)
                    .Distinct()
                    .ToList();

                var vendorOrders = _context.OrderTables
                    .Where(o => (o.VendorId == v.VendorId || orderIds.Contains(o.OrderId)) && o.CreatedAt >= start && o.CreatedAt < endExclusive)
                    .ToList();

                var ordersCount = vendorOrders.Count;
                
                var revenue = vendorOrders.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? o.VendorAmount : o.TotalAmount * 0.9m);

                var cancelled = vendorOrders.Count(o => o.Status == "Cancelled");

                decimal cancelRate = (ordersCount > 0) ? Math.Round((decimal)cancelled / ordersCount * 100m, 2) : 0m;

                vendorRows.Add(new Models.Reports.VendorRow
                {
                    VendorId = v.VendorId,
                    VendorName = v.VendorName,
                    Orders = ordersCount,
                    Revenue = Math.Round(revenue, 2),
                    CancellationRate = cancelRate,
                    Performance = (revenue > 50000 && cancelRate < 4) ? "Good" : (cancelRate > 8 || revenue < 8000 ? "Poor" : "Average")
                });
            }


            var ranked = vendorRows.OrderByDescending(v => v.Revenue).ToArray();
            var topVendors = ranked.Take(top).ToArray();

            var summary = new Models.Reports.VendorSummary
            {
                TotalVendors = vendorRows.Count,
                ActiveVendors = vendorRows.Count(v => v.Orders > 0),
                TotalRevenue = Math.Round(vendorRows.Sum(v => v.Revenue), 2),
                TotalOrders = vendorRows.Sum(v => v.Orders)
            };

            return Json(new Models.Reports.VendorPerformanceModel
            {
                Summary = summary,
                Vendors = topVendors,
                Chart = new Models.Reports.ChartData
                {
                    Labels = topVendors.Select(v => v.VendorName).ToArray(),
                    Values = topVendors.Select(v => v.Revenue).ToArray()
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData(string period = "monthly", DateTime? refDate = null, bool noCache = false)
        {
            const string cacheKeyPrefix = "EnhancedDashboardData_";
            DateTime referenceDate = refDate ?? DateTime.Today;
            string cacheKey = cacheKeyPrefix + (period ?? "monthly") + "_" + referenceDate.ToString("yyyyMMdd");
            
            // if (!noCache && _cache.TryGetValue(cacheKey, out object cachedData)) return Json(cachedData);

            try
            {
                var periodLower = period?.ToLowerInvariant() ?? "monthly";
                DateTime rangeStart = referenceDate;
                DateTime rangeEnd = referenceDate.AddDays(1);

                if (periodLower == "daily") { rangeStart = referenceDate.Date; rangeEnd = rangeStart.AddDays(1); }
                else if (periodLower == "weekly") { rangeStart = referenceDate.Date.AddDays(-(int)referenceDate.DayOfWeek); rangeEnd = rangeStart.AddDays(7); }
                else if (periodLower == "monthly") { rangeStart = new DateTime(referenceDate.Year, referenceDate.Month, 1); rangeEnd = rangeStart.AddMonths(1); }
                else if (periodLower == "yearly") { rangeStart = new DateTime(referenceDate.Year, 1, 1); rangeEnd = rangeStart.AddYears(1); }
                else if (periodLower == "alltime") { rangeStart = new DateTime(2020, 1, 1); rangeEnd = DateTime.Now.AddDays(1); }

                var ordersInPeriod = await _context.OrderTables
                    .Where(o => o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd)
                    .ToListAsync();

                var todaysOrders = ordersInPeriod.Count;
                var todaysRevenue = (decimal)ordersInPeriod.Where(o => o.Status != "Cancelled").Sum(o => (double)o.TotalAmount);
                var todaysCommission = (decimal)ordersInPeriod.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? (double)o.CommissionAmount : (double)o.TotalAmount * 0.1);

                var totalUsers = await _context.UserSignups.CountAsync();
                var userGrowth = await _context.UserSignups.CountAsync(u => u.CreatedAt >= rangeStart && u.CreatedAt < rangeEnd);
                var activeVendors = await _context.VendorSignups.CountAsync(v => v.IsApproved == true);
                var totalCategories = await _context.Foods.Select(f => f.CategoryId).Distinct().CountAsync();
                var activeDeliveries = await _context.OrderTables.CountAsync(o => o.Status == "In Transit" || o.Status == "Out for Delivery" || o.Status == "Accepted");
                
                var totalEarned = (decimal)ordersInPeriod.Where(o => o.Status != "Cancelled").Sum(o => o.VendorAmount > 0 ? (double)o.VendorAmount : (double)o.TotalAmount * 0.9);
                var totalPaidToVendors = await _context.VendorPayouts
                    .Where(p => p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd && p.Status == PayoutStatus.PaidToVendor)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                var pendingPayouts = totalEarned - totalPaidToVendors;
                if (pendingPayouts < 0) pendingPayouts = 0;

                var totalLoss = (decimal)ordersInPeriod.Where(o => o.Status == "Cancelled").Sum(o => (double)o.TotalAmount);

                var trendLabels = new List<string>();
                var revenueData = new List<double>();
                var profitData = new List<double>();
                var lossData = new List<double>();
                var orderData = new List<int>();
                var vendorEarnedData = new List<double>();
                var adminPaidData = new List<double>();

                if (periodLower == "monthly")
                {
                    // Show last 12 months including current
                    for (int i = 11; i >= 0; i--)
                    {
                        var d = referenceDate.AddMonths(-i);
                        var monthStart = new DateTime(d.Year, d.Month, 1);
                        var monthEnd = monthStart.AddMonths(1);
                        
                        trendLabels.Add(d.ToString("MMM yy"));
                        var orders = await _context.OrderTables.Where(o => o.CreatedAt >= monthStart && o.CreatedAt < monthEnd).ToListAsync();
                        
                        var rev = orders.Where(o => o.Status != "Cancelled").Sum(o => (double)o.TotalAmount);
                        var prof = orders.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? (double)o.CommissionAmount : (double)o.TotalAmount * 0.1);
                        var loss = orders.Where(o => o.Status == "Cancelled").Sum(o => (double)o.TotalAmount);
                        var paid = await _context.VendorPayouts.Where(p => p.CreatedAt >= monthStart && p.CreatedAt < monthEnd && p.Status == PayoutStatus.PaidToVendor).SumAsync(p => (double?)p.Amount) ?? 0;

                        revenueData.Add(rev); 
                        profitData.Add(prof); 
                        lossData.Add(loss);
                        vendorEarnedData.Add(rev - prof);
                        adminPaidData.Add(paid);
                        orderData.Add(orders.Count);
                    }
                }
                else if (periodLower == "yearly")
                {
                    // Show last 5 years
                    for (int i = 4; i >= 0; i--)
                    {
                        var d = referenceDate.AddYears(-i);
                        var yearStart = new DateTime(d.Year, 1, 1);
                        var yearEnd = yearStart.AddYears(1);
                        
                        trendLabels.Add(d.Year.ToString());
                        var orders = await _context.OrderTables.Where(o => o.CreatedAt >= yearStart && o.CreatedAt < yearEnd).ToListAsync();
                        
                        var rev = orders.Where(o => o.Status != "Cancelled").Sum(o => (double)o.TotalAmount);
                        var prof = orders.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? (double)o.CommissionAmount : (double)o.TotalAmount * 0.1);
                        var loss = orders.Where(o => o.Status == "Cancelled").Sum(o => (double)o.TotalAmount);
                        var paid = await _context.VendorPayouts.Where(p => p.CreatedAt >= yearStart && p.CreatedAt < yearEnd && p.Status == PayoutStatus.PaidToVendor).SumAsync(p => (double?)p.Amount) ?? 0;

                        revenueData.Add(rev); 
                        profitData.Add(prof); 
                        lossData.Add(loss);
                        vendorEarnedData.Add(rev - prof);
                        adminPaidData.Add(paid);
                        orderData.Add(orders.Count);
                    }
                }
                else if (periodLower == "weekly")
                {
                    DateTime startOfWeek = rangeStart;
                    for (int i = 0; i < 7; i++)
                    {
                        var d = startOfWeek.AddDays(i);
                        trendLabels.Add(d.ToString("ddd"));
                        var orders = await _context.OrderTables.Where(o => o.CreatedAt >= d.Date && o.CreatedAt < d.Date.AddDays(1)).ToListAsync();
                        
                        var rev = orders.Where(o => o.Status != "Cancelled").Sum(o => (double?)o.TotalAmount) ?? 0;
                        var prof = orders.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? (double?)o.CommissionAmount : (double?)(o.TotalAmount * 0.1m)) ?? 0;
                        var loss = orders.Where(o => o.Status == "Cancelled").Sum(o => (double?)o.TotalAmount) ?? 0;
                        var paid = await _context.VendorPayouts.Where(p => p.CreatedAt >= d.Date && p.CreatedAt < d.Date.AddDays(1) && p.Status == PayoutStatus.PaidToVendor).SumAsync(p => (double?)p.Amount) ?? 0;

                        revenueData.Add(rev); 
                        profitData.Add(prof); 
                        lossData.Add(loss);
                        vendorEarnedData.Add(rev - prof);
                        adminPaidData.Add(paid);
                        orderData.Add(orders.Count);
                    }
                }
                else if (periodLower == "daily")
                {
                    for (int i = 0; i < 24; i++)
                    {
                        var hStart = rangeStart.Date.AddHours(i);
                        var hEnd = hStart.AddHours(1);
                        trendLabels.Add($"{i}:00");
                        
                        var orders = await _context.OrderTables.Where(o => o.CreatedAt >= hStart && o.CreatedAt < hEnd).ToListAsync();
                        
                        var rev = orders.Where(o => o.Status != "Cancelled").Sum(o => (double?)o.TotalAmount) ?? 0;
                        var prof = orders.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? (double?)o.CommissionAmount : (double?)(o.TotalAmount * 0.1m)) ?? 0;
                        var loss = orders.Where(o => o.Status == "Cancelled").Sum(o => (double?)o.TotalAmount) ?? 0;
                        var paid = await _context.VendorPayouts.Where(p => p.CreatedAt >= hStart && p.CreatedAt < hEnd && p.Status == PayoutStatus.PaidToVendor).SumAsync(p => (double?)p.Amount) ?? 0;

                        revenueData.Add(rev); 
                        profitData.Add(prof); 
                        lossData.Add(loss);
                        vendorEarnedData.Add(rev - prof);
                        adminPaidData.Add(paid);
                        orderData.Add(orders.Count);
                    }
                }
                else if (periodLower == "alltime")
                {
                    // For all-time, show yearly trend for last 5 years
                    for (int i = -4; i <= 0; i++)
                    {
                        var d = DateTime.Today.AddYears(i);
                        trendLabels.Add(d.Year.ToString());
                        var orders = await _context.OrderTables.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Year == d.Year).ToListAsync();
                        
                        var rev = orders.Where(o => o.Status != "Cancelled").Sum(o => (double?)o.TotalAmount) ?? 0;
                        var prof = orders.Where(o => o.Status != "Cancelled").Sum(o => o.CommissionAmount > 0 ? (double?)o.CommissionAmount : (double?)(o.TotalAmount * 0.1m)) ?? 0;
                        var loss = orders.Where(o => o.Status == "Cancelled").Sum(o => (double?)o.TotalAmount) ?? 0;
                        var paid = await _context.VendorPayouts.Where(p => p.CreatedAt.Year == d.Year && p.Status == PayoutStatus.PaidToVendor).SumAsync(p => (double?)p.Amount) ?? 0;

                        revenueData.Add(rev); 
                        profitData.Add(prof); 
                        lossData.Add(loss);
                        vendorEarnedData.Add(rev - prof);
                        adminPaidData.Add(paid);
                        orderData.Add(orders.Count);
                    }
                }

                // Top Vendors in this period with detail
                var topVendors = await _context.OrderTables
                    .Where(o => o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd && o.Status != "Cancelled" && o.VendorId != null)
                    .GroupBy(o => o.VendorId)
                    .Select(g => new { 
                        VendorId = g.Key, 
                        Sales = g.Sum(o => (decimal)o.TotalAmount), 
                        Commission = g.Sum(o => o.CommissionAmount > 0 ? (decimal)o.CommissionAmount : (decimal)o.TotalAmount * 0.1m),
                        Orders = g.Count() 
                    })
                    .OrderByDescending(x => x.Sales)
                    .Take(5)
                    .ToListAsync();

                var vendorIds = topVendors.Select(v => v.VendorId).ToList();
                var vendorNames = await _context.VendorSignups
                    .Where(v => vendorIds.Contains(v.VendorId))
                    .ToDictionaryAsync(v => v.VendorId, v => v.VendorName);

                // Get payouts for these vendors in the period
                var vendorPayouts = await _context.VendorPayouts
                    .Where(p => vendorIds.Contains(p.VendorId) && p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd && p.Status == PayoutStatus.PaidToVendor)
                    .GroupBy(p => p.VendorId)
                    .Select(g => new { VendorId = g.Key, Paid = g.Sum(p => (decimal)p.Amount) })
                    .ToDictionaryAsync(x => x.VendorId, x => x.Paid);

                var topVendorsList = topVendors.Select(v => new {
                    name = vendorNames.ContainsKey(v.VendorId ?? 0) ? vendorNames[v.VendorId ?? 0] : "Unknown Vendor",
                    sales = v.Sales,
                    earned = v.Sales - v.Commission,
                    paid = vendorPayouts.ContainsKey(v.VendorId ?? 0) ? vendorPayouts[v.VendorId ?? 0] : 0m,
                    orders = v.Orders
                }).ToList();

                // Stock alerts (items near limit)
                var lowStockItems = await _context.Foods
                    .Where(f => f.DailyLimit.HasValue && f.DailyLimit.Value > 0)
                    .Select(f => new {
                        name = f.Name ?? "Unknown Item",
                        limit = f.DailyLimit.Value,
                        sold = _context.OrderItems.Where(oi => oi.ItemName == f.Name && oi.CreatedAt >= DateTime.Today).Sum(oi => (int?)oi.Quantity) ?? 0
                    })
                    .OrderByDescending(x => (double)x.sold / x.limit)
                    .Take(5)
                    .ToListAsync();

                // User growth in this period
                var userReg = new List<int>();
                if (periodLower == "monthly")
                {
                    for (int i = 0; i < 12; i++)
                    {
                        var d = new DateTime(referenceDate.Year, 1, 1).AddMonths(i);
                        userReg.Add(await _context.UserSignups.CountAsync(u => u.CreatedAt.HasValue && u.CreatedAt.Value.Year == d.Year && u.CreatedAt.Value.Month == d.Month));
                    }
                }
                else if (periodLower == "weekly")
                {
                    for (int i = 0; i < 7; i++)
                    {
                        var d = rangeStart.Date.AddDays(i);
                        userReg.Add(await _context.UserSignups.CountAsync(u => u.CreatedAt.HasValue && u.CreatedAt.Value.Date == d.Date));
                    }
                }
                else if (periodLower == "daily")
                {
                    for (int i = 0; i < 24; i++)
                    {
                        var h = rangeStart.Date.AddHours(i);
                        userReg.Add(await _context.UserSignups.CountAsync(u => u.CreatedAt.HasValue && u.CreatedAt.Value >= h && u.CreatedAt.Value < h.AddHours(1)));
                    }
                }
                else
                {
                    userReg = trendLabels.Select(_ => 0).ToList();
                }

                var statusDistRaw = await _context.OrderTables
                    .Where(o => o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd)
                    .GroupBy(o => o.Status)
                    .Select(g => new { status = g.Key ?? "New", count = g.Count() })
                    .ToListAsync();
                
                var statusDist = statusDistRaw.Select(x => (dynamic)x).ToList();

                 // Top Categories
                 var topCategoriesRaw = await _context.OrderItems
                     .Where(oi => _context.OrderTables.Any(o => o.OrderId == oi.OrderId && o.CreatedAt >= rangeStart && o.CreatedAt < rangeEnd))
                     .GroupBy(oi => oi.ItemName)
                     .Select(g => new { name = g.Key ?? "Unknown", count = g.Sum(x => (int?)x.Quantity) ?? 0 })
                     .OrderByDescending(x => x.count)
                     .Take(8)
                     .ToListAsync();
                 
                 var topCategories = topCategoriesRaw.Select(x => (dynamic)x).ToList();

                 var hasAnyOrdersInDb = await _context.OrderTables.AnyAsync();
                 var activeSubscriptions = await _context.Subscriptions.CountAsync(s => s.Status == "Active");

                 if (!hasAnyOrdersInDb)
                 {
                    // Full Sample Data Fallback (Demo Mode) if DB is totally empty
                    var rnd = new Random();
                    todaysOrders = 42;
                    todaysRevenue = 15420.50m;
                    todaysCommission = 1542.05m;
                    totalUsers = 156;
                    activeVendors = 8;
                    totalCategories = 14;
                    activeDeliveries = 3;
                    pendingPayouts = 4500.00m;
                    totalLoss = 850.00m;

                    // Mock Trend for empty DB
                    for (int i = 0; i < trendLabels.Count; i++)
                    {
                        var rev = 1200 + rnd.Next(200, 800);
                        revenueData[i] = rev;
                        profitData[i] = rev * 0.1;
                        lossData[i] = rnd.Next(0, 200);
                        orderData[i] = rnd.Next(5, 15);
                        vendorEarnedData[i] = rev * 0.9;
                        adminPaidData[i] = rev * 0.7;
                    }
                 }

                 var response = new { 
                     success = true, 
                     summary = new { 
                         todaysOrders, 
                         activeSubscriptions,
                         todaysRevenue, 
                         todaysCommission, 
                         totalUsers, 
                         activeVendors, 
                         pendingPayouts, 
                         totalLoss, 
                         totalCategories,
                          activeDeliveries,
                         avgDeliveryTime = 0.0,
                         systemHealth = 100.0,
                         activeSessions = hasAnyOrdersInDb ? 0 : 12,
                         conversionRate = totalUsers > 0 ? Math.Round((double)todaysOrders / totalUsers * 100, 1) : 4.2,
                         topVendors = hasAnyOrdersInDb ? topVendorsList.Cast<object>().ToList() : new List<object> {
                            new { name = "Biryani House", sales = 4500m, earned = 4050m, paid = 3500m, orders = 12 },
                            new { name = "Healthy Salads", sales = 3200m, earned = 2880m, paid = 2000m, orders = 8 },
                            new { name = "Punjabi Tadka", sales = 2800m, earned = 2520m, paid = 2520m, orders = 6 }
                         },
                         lowStockItems = hasAnyOrdersInDb ? lowStockItems.Cast<object>().ToList() : new List<object> {
                            new { name = "Paneer Tikka", sold = 18, limit = 20 },
                            new { name = "Chicken Biryani", sold = 14, limit = 15 }
                         }
                     },
                    charts = new { 
                        labels = trendLabels, 
                        revenue = revenueData, 
                        profit = profitData, 
                        loss = lossData, 
                        orders = orderData,
                        userReg = userReg,
                        statusDist = hasAnyOrdersInDb ? statusDist.Cast<object>().ToList() : new List<object> {
                            new { status = "Delivered", count = 25 },
                            new { status = "Accepted", count = 10 },
                            new { status = "Cancelled", count = 5 }
                        },
                        categories = hasAnyOrdersInDb ? topCategories.Cast<object>().ToList() : new List<object> {
                            new { name = "Main Course", count = 45 },
                            new { name = "Snacks", count = 30 },
                            new { name = "Beverages", count = 15 }
                        },
                        vendorEarned = vendorEarnedData,
                        vendorPaid = adminPaidData
                    },
                    alerts = hasAnyOrdersInDb 
                        ? (await _context.ActivityLogs.OrderByDescending(l => l.Timestamp).Take(5).Select(l => new { time = l.Timestamp.ToString("HH:mm"), type = l.Action, message = l.Details }).ToListAsync()).Cast<object>().ToList()
                        : new List<object> {
                            new { time = "10:30", type = "Order", message = "New order #4521 received" },
                            new { time = "09:15", type = "Payment", message = "Payout of ₹2500 processed" }
                        }
                };
                
                _cache.Set(cacheKey, response, TimeSpan.FromSeconds(30));
                return Json(response);
            }
            catch (Exception ex) 
            { 
                System.IO.File.WriteAllText("error.txt", ex.ToString());
                return Json(new { success = false, message = ex.Message }); 
            }
        }

    }
}
