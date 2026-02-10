export interface Sale {
    id: number;
    bookId: number;
    bookTitle: string;
    platformId: number;
    platformName: string;
    saleDate: Date;
    quantity: number;
    unitPrice: number;
    royalty: number;
    revenue: number;
}

export interface CreateSale {
    bookId: number;
    platformId: number;
    saleDate: string;
    quantity: number;
    unitPrice: number;
    royalty: number;
    currency: string;
}

export interface SalesSummary {
    totalRevenue: number;
    totalUnitsSold: number;
    averageRoyalty: number;
    salesCount: number;
}

export interface SalesFilter {
    bookId?: number;
    platformId?: number;
    startDate?: string;
    endDate?: string;
}
