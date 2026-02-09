import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { PlatformService } from './platform.service';
import { Platform, PlatformRequest } from '../models/platform';

describe('PlatformService', () => {
  let service: PlatformService;
  let httpMock: HttpTestingController;
  const apiUrl = '/api/platforms';

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [PlatformService]
    });
    service = TestBed.inject(PlatformService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get platforms', () => {
    const mockPlatforms: Platform[] = [{ id: 1, name: 'Amazon KDP', baseUrl: 'https://kdp.amazon.com', commissionRate: 0.3 }];
    service.getPlatforms().subscribe(platforms => {
      expect(platforms).toEqual(mockPlatforms);
    });
    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('GET');
    req.flush(mockPlatforms);
  });

  it('should request a platform', () => {
    const newPlatformRequest: PlatformRequest = { name: 'New Platform', baseUrl: 'https://newplatform.com', commissionRate: 0.4 };
    service.requestPlatform(newPlatformRequest).subscribe();
    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(newPlatformRequest);
    req.flush({});
  });
});