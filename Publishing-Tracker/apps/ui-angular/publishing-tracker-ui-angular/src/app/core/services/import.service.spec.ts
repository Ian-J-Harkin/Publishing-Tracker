import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ImportService } from './import.service';
import { ImportJob, ColumnMapping, PreviewData } from '../models/import';

describe('ImportService', () => {
    let service: ImportService;
    let httpMock: HttpTestingController;
    const apiUrl = '/api/import';

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [ImportService]
        });
        service = TestBed.inject(ImportService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should upload a file and return preview data', () => {
        const mockPreview: PreviewData = {
            fileName: 'sales.csv',
            headers: ['Book Title', 'Platform', 'Sale Date', 'Quantity', 'Unit Price'],
            previewRows: [{ 'Book Title': 'Test Book', 'Platform': 'Amazon KDP' }]
        };

        const file = new File(['csv,content'], 'sales.csv', { type: 'text/csv' });

        service.uploadFile(file).subscribe(preview => {
            expect(preview).toEqual(mockPreview);
            expect(preview.headers.length).toBe(5);
        });

        const req = httpMock.expectOne(`${apiUrl}/upload`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body instanceof FormData).toBeTrue();
        req.flush(mockPreview);
    });

    it('should process a file with column mapping', () => {
        const mapping: ColumnMapping = {
            bookTitle: 'Book Title',
            platform: 'Platform',
            saleDate: 'Sale Date',
            quantity: 'Quantity',
            unitPrice: 'Unit Price',
            royalty: 'Royalty',
            revenue: '',
            currency: 'Currency',
            orderId: 'Order ID'
        };

        const mockResult: ImportJob = {
            id: 1,
            fileName: 'sales.csv',
            status: 'Completed',
            startedAt: new Date('2024-01-01'),
            recordsProcessed: 10,
            recordsSuccessful: 8,
            recordsFailed: 2,
            errorLog: 'Row 3: Invalid date'
        };

        service.processFile('sales.csv', mapping).subscribe(result => {
            expect(result).toEqual(mockResult);
            expect(result.recordsSuccessful).toBe(8);
        });

        const req = httpMock.expectOne(`${apiUrl}/process`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body.fileName).toBe('sales.csv');
        expect(req.request.body.mapping).toEqual(mapping);
        req.flush(mockResult);
    });

    it('should get import history', () => {
        const mockHistory: ImportJob[] = [
            {
                id: 1, fileName: 'sales_q1.csv', status: 'Completed',
                startedAt: new Date('2024-01-01'), recordsProcessed: 100,
                recordsSuccessful: 95, recordsFailed: 5
            },
            {
                id: 2, fileName: 'sales_q2.csv', status: 'Failed',
                startedAt: new Date('2024-04-01'), recordsProcessed: 50,
                recordsSuccessful: 0, recordsFailed: 50,
                errorLog: 'Connection timeout'
            }
        ];

        service.getHistory().subscribe(history => {
            expect(history.length).toBe(2);
            expect(history[0].status).toBe('Completed');
            expect(history[1].status).toBe('Failed');
        });

        const req = httpMock.expectOne(`${apiUrl}/history`);
        expect(req.request.method).toBe('GET');
        req.flush(mockHistory);
    });
});
