using System;
using System.Collections.Generic;

namespace PublishingTracker.Api.Models.Dtos
{
    public class DashboardSummaryDto
    {
        public List<CurrencyTotalDto> RevenueByCurrency { get; set; } = new();
        public int TotalBooksPublished { get; set; }
        public int TotalSalesTransactions { get; set; }
        public string TopPerformingBook { get; set; } = string.Empty;
        public string TopPerformingPlatform { get; set; } = string.Empty;
    }

    public class DashboardDataDto
    {
        public DashboardSummaryDto Summary { get; set; } = new();
        public dynamic YoY { get; set; } = null!;
        public dynamic Seasonal { get; set; } = null!;
    }

    public class RevenueChartDataDto
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Revenues { get; set; } = new();
    }

    public class PlatformPerformanceDto
    {
        public string PlatformName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public double PercentageOfTotal { get; set; }
    }
}