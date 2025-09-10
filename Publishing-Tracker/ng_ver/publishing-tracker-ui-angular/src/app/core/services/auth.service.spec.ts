import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { Router } from '@angular/router';

import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule, RouterTestingModule.withRoutes([])],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('register', () => {
    it('should send a POST request to the register endpoint', () => {
      const mockUser = { username: 'test', password: 'password' };
      service.register(mockUser).subscribe();

      const req = httpMock.expectOne('http://localhost:5226/api/auth/register');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(mockUser);
      req.flush({});
    });
  });

  describe('login', () => {
    it('should send a POST request to the login endpoint and store the token', () => {
      const mockUser = { username: 'test', password: 'password' };
      const mockResponse = { token: 'test-token' };
      spyOn(localStorage, 'setItem');

      service.login(mockUser).subscribe();

      const req = httpMock.expectOne('http://localhost:5226/api/auth/login');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(mockUser);
      req.flush(mockResponse);

      expect(localStorage.setItem).toHaveBeenCalledWith('token', 'test-token');
    });
  });

  describe('logout', () => {
    it('should remove the token from localStorage and navigate to /login', () => {
      spyOn(localStorage, 'removeItem');
      spyOn(router, 'navigate');

      service.logout();

      expect(localStorage.removeItem).toHaveBeenCalledWith('token');
      expect(router.navigate).toHaveBeenCalledWith(['/login']);
    });
  });
});