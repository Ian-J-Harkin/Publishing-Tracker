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
    saleDate: Date;
    quantity: number;
    unitPrice: number;
    royalty: number;
}