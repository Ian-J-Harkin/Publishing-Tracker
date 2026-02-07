export interface Book {
    id: number;
    title: string;
    author: string;
    isbn?: string;
    publicationDate?: Date;
    basePrice?: number;
    genre?: string;
    description?: string;
}

export interface CreateBook {
    title: string;
    author: string;
    isbn?: string;
    publicationDate?: Date;
    basePrice?: number;
    genre?: string;
    description?: string;
}

export interface UpdateBook {
    title?: string;
    author?: string;
    isbn?: string;
    publicationDate?: Date;
    basePrice?: number;
    genre?: string;
    description?: string;
}

export interface BookPerformance {
    date: string;
    revenue: number;
    salesCount: number;
}