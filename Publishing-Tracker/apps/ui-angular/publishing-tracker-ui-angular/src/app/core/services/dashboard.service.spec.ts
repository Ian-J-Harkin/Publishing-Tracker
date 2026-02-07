import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { DashboardSummary, YoYComparison, SeasonalPerformance } from '../models/dashboard';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;
  const apiUrl = '/api/dashboard';

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [DashboardService]
    });
    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
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
    req.flush(mockSummary);
  });

  it('should get YoY comparison', () => {
    const mockComparison: YoYComparison = { lastYearRevenue: 500, currentYearRevenue: 1000, growth: 1.0 };
    service.getYoYComparison().subscribe(comparison => {
      expect(comparison).toEqual(mockComparison);
    });
    const req = httpMock.expectOne(`${apiUrl}/yoy`);
    expect(req.request.method).toBe('GET');
    req.flush(mockComparison);
  });

  it('should get seasonal performance', () => {
    const mockPerformance: SeasonalPerformance[] = [{ month: 1, totalRevenue: 100 }, { month: 2, totalRevenue: 200 }];
    service.getSeasonalPerformance().subscribe(performance => {
      expect(performance).toEqual(mockPerformance);
    });
    const req = httpMock.expectOne(`${apiUrl}/seasonal`);
    expect(req.request.method).toBe('GET');
    req.flush(mockPerformance);
  });
});