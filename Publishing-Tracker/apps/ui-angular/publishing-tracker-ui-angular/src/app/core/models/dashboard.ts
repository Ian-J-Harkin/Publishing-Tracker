export interface DashboardSummary {
    totalRevenue: number;
    totalBooksPublished: number;
    totalSalesTransactions: number;
    topPerformingBook: string;
    topPerformingPlatform: string;
}

export interface RevenueChartData {
    labels: string[];
    revenues: number[];
}

export interface PlatformPerformance {
    platformName: string;
    totalRevenue: number;
    percentageOfTotal: number;
}

export interface YoYComparison {
    currentYearRevenue: number;
    lastYearRevenue: number;
    growth: number;
}

export interface SeasonalPerformance {
    month: number;
    totalRevenue: number;
}