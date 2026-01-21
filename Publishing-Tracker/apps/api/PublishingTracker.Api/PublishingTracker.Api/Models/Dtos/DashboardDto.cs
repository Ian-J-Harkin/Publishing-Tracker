using System;
using System.Collections.Generic;

namespace PublishingTracker.Api.Models.Dtos
{
    public class DashboardSummaryDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalBooksPublished { get; set; }
        public int TotalSalesTransactions { get; set; }
        public string TopPerformingBook { get; set; } = string.Empty;
        public string TopPerformingPlatform { get; set; } = string.Empty;
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