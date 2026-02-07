export interface CurrencyTotal {
    currency: string;
    totalAmount: number;
}

export interface DashboardSummary {
    revenueByCurrency: CurrencyTotal[];
    totalBooksPublished: number;
    totalSalesTransactions: number;
    topPerformingBook: string;
    topPerformingPlatform: string;
}

export interface DashboardData {
    summary: DashboardSummary;
    yoY: YoYComparison;
    seasonal: SeasonalPerformance[];
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