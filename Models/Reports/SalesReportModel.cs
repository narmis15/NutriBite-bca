namespace NUTRIBITE.Models.Reports
{
    public class SalesReportModel
    {
        // Summary
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal Profit { get; set; }
        public decimal TotalLoss { get; set; }

        // Trend points for chart (re-uses shared TrendPoint)
        public TrendPoint[] Trend { get; set; } = Array.Empty<TrendPoint>();

        // Breakdown rows (daily/monthly)
        public BreakdownRow[] Breakdown { get; set; } = Array.Empty<BreakdownRow>();
    }

    public class BreakdownRow
    {
        public string PeriodLabel { get; set; } = "";
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal AvgOrderValue { get; set; }
        public decimal Profit { get; set; }
        public decimal Loss { get; set; }
    }
}
