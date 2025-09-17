import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { PlatformService } from './platform.service';
import { Platform, PlatformRequest } from '../models/platform';

describe('PlatformService', () => {
  let service: PlatformService;
  let httpMock: HttpTestingController;
  const apiUrl = import.meta.env.VITE_API_BASE_URL + '/api/platforms';
  const mockToken = 'test-token';

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [PlatformService]
    });
    service = TestBed.inject(PlatformService);
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(localStorage, 'getItem').and.returnValue(mockToken);
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
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockPlatforms);
  });

  it('should request a platform', () => {
    const newPlatformRequest: PlatformRequest = { name: 'New Platform', baseUrl: 'https://newplatform.com', commissionRate: 0.4 };
    service.requestPlatform(newPlatformRequest).subscribe();
    const req = httpMock.expectOne(`${apiUrl}/requests`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(newPlatformRequest);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush({});
  });
});