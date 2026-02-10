import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { SaleService } from './sale.service';
import { Sale, CreateSale, SalesSummary } from '../models/sale';

describe('SaleService', () => {
    let service: SaleService;
    let httpMock: HttpTestingController;
    const apiUrl = '/api/sales';

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [SaleService]
        });
        service = TestBed.inject(SaleService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should get sales without filters', () => {
        const mockSales: Sale[] = [{
            id: 1, bookId: 1, bookTitle: 'Test Book', platformId: 1,
            platformName: 'Amazon KDP', saleDate: new Date('2024-01-01'),
            quantity: 5, unitPrice: 9.99, royalty: 3.50, revenue: 17.50
        }];

        service.getSales().subscribe(sales => {
            expect(sales).toEqual(mockSales);
        });

        const req = httpMock.expectOne(apiUrl);
        expect(req.request.method).toBe('GET');
        req.flush(mockSales);
    });

    it('should get sales with filters as query params', () => {
        service.getSales({ bookId: 1, startDate: '2024-01-01' }).subscribe();

        const req = httpMock.expectOne(r =>
            r.url === apiUrl &&
            r.params.get('bookId') === '1' &&
            r.params.get('startDate') === '2024-01-01'
        );
        expect(req.request.method).toBe('GET');
        req.flush([]);
    });

    it('should strip empty filter values from params', () => {
        service.getSales({ bookId: undefined, startDate: '', endDate: '2024-12-31' }).subscribe();

        const req = httpMock.expectOne(r => r.url === apiUrl);
        expect(req.request.params.has('bookId')).toBeFalse();
        expect(req.request.params.has('startDate')).toBeFalse();
        expect(req.request.params.get('endDate')).toBe('2024-12-31');
        req.flush([]);
    });

    it('should get sales summary', () => {
        const mockSummary: SalesSummary = {
            totalRevenue: 1000, totalUnitsSold: 50,
            averageRoyalty: 3.50, salesCount: 50
        };

        service.getSummary().subscribe(summary => {
            expect(summary).toEqual(mockSummary);
        });

        const req = httpMock.expectOne(`${apiUrl}/summary`);
        expect(req.request.method).toBe('GET');
        req.flush(mockSummary);
    });

    it('should create a sale', () => {
        const newSale: CreateSale = {
            bookId: 1, platformId: 2, saleDate: '2024-01-15',
            quantity: 3, unitPrice: 12.99, royalty: 4.55, currency: 'USD'
        };
        const createdSale: Sale = {
            id: 10, bookId: 1, bookTitle: 'Test', platformId: 2,
            platformName: 'Kobo', saleDate: new Date('2024-01-15'),
            quantity: 3, unitPrice: 12.99, royalty: 4.55, revenue: 13.65
        };

        service.createSale(newSale).subscribe(sale => {
            expect(sale).toEqual(createdSale);
        });

        const req = httpMock.expectOne(apiUrl);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(newSale);
        req.flush(createdSale);
    });
});
