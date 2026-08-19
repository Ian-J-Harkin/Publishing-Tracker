import { Book, CreateBook, UpdateBook, BookPerformance } from '../types/book';
import { ImportJob, ColumnMapping, PreviewData } from '../types/import';
import { Platform, PlatformRequest } from '../types/platform';
import { Sale, CreateSale, SalesSummary, SalesFilter } from '../types/sale';
import { DashboardSummary, YoYComparison, SeasonalPerformance, DashboardData } from '../types/dashboard';
import { User } from '../adapters/UserAxiosAdapter'; // TODO: Move User interface to types/user.ts

export interface IAuthPort {
    register(data: Record<string, unknown>): Promise<any>;
    login(data: Record<string, unknown>): Promise<any>;
}

export interface IBookPort {
    getBooks(search?: string): Promise<Book[]>;
    getBookById(id: number): Promise<Book>;
    createBook(book: CreateBook): Promise<Book>;
    updateBook(id: number, book: UpdateBook): Promise<Book>;
    deleteBook(id: number): Promise<void>;
    getBookPerformance(id: number): Promise<BookPerformance[]>;
}

export interface IImportPort {
    uploadFile(file: File): Promise<PreviewData>;
    processFile(fileName: string, mapping: ColumnMapping): Promise<ImportJob>;
    getHistory(): Promise<ImportJob[]>;
}

export interface IPlatformPort {
    getPlatforms(search?: string): Promise<Platform[]>;
    getAdminPlatforms(search?: string): Promise<Platform[]>;
    requestPlatform(platformRequest: PlatformRequest): Promise<void>;
    approvePlatform(id: number): Promise<void>;
}

export interface ISalePort {
    getSales(filters?: SalesFilter): Promise<Sale[]>;
    getSummary(filters?: { startDate?: string; endDate?: string }): Promise<SalesSummary>;
    createSale(sale: CreateSale): Promise<Sale>;
}

export interface IDashboardPort {
    getDashboardData(): Promise<DashboardData>;
    getDashboardSummary(): Promise<DashboardSummary>;
    getYoYComparison(): Promise<YoYComparison>;
    getSeasonalPerformance(): Promise<SeasonalPerformance[]>;
}

export interface IUserPort {
    getUsers(): Promise<User[]>;
    promoteUser(id: number): Promise<void>;
    demoteUser(id: number): Promise<void>;
    getProfile(): Promise<User>;
    updateProfile(firstName: string, lastName: string): Promise<void>;
    changePassword(oldPassword: string, newPassword: string): Promise<void>;
}

export interface IDependencies {
    auth: IAuthPort;
    books: IBookPort;
    imports: IImportPort;
    platforms: IPlatformPort;
    sales: ISalePort;
    dashboard: IDashboardPort;
    users: IUserPort;
}
