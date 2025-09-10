import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { DashboardSummary } from '../models/dashboard';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;
  const apiUrl = 'http://localhost:5000/api/dashboard';
  const mockToken = 'test-token';

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [DashboardService]
    });
    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(localStorage, 'getItem').and.returnValue(mockToken);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get dashboard summary', () => {
    const mockSummary: DashboardSummary = {
      totalRevenue: 1000,
      totalBooksPublished: 10,
      totalSalesTransactions: 100,
      topPerformingBook: 'Test Book',
      topPerformingPlatform: 'Test Platform'
    };
    service.getDashboardSummary().subscribe(summary => {
      expect(summary).toEqual(mockSummary);
    });
    const req = httpMock.expectOne(`${apiUrl}/summary`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockSummary);
  });

  it('should get YoY comparison', () => {
    const mockComparison = { lastYear: 500, thisYear: 1000 };
    service.getYoYComparison().subscribe(comparison => {
      expect(comparison).toEqual(mockComparison);
    });
    const req = httpMock.expectOne(`${apiUrl}/yoy`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockComparison);
  });

  it('should get seasonal performance', () => {
    const mockPerformance = { spring: 100, summer: 200, fall: 300, winter: 400 };
    service.getSeasonalPerformance().subscribe(performance => {
      expect(performance).toEqual(mockPerformance);
    });
    const req = httpMock.expectOne(`${apiUrl}/seasonal`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockPerformance);
  });
});