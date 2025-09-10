export interface ImportJob {
    id: number;
    fileName: string;
    status: string;
    startedAt: Date;
    completedAt?: Date;
    recordsProcessed: number;
    recordsSuccessful: number;
    recordsFailed: number;
    errorLog?: string;
}

export interface ColumnMapping {
    bookTitle: string;
    platform: string;
    saleDate: string;
    quantity: string;
    unitPrice: string;
    royalty: string;
}