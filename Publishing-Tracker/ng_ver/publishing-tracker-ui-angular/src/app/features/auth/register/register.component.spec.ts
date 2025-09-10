import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { Router } from '@angular/router';

import { RegisterComponent } from './register.component';
import { AuthService } from '../../../core/services/auth.service';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    const authServiceSpy = jasmine.createSpyObj('AuthService', ['register']);

    await TestBed.configureTestingModule({
      imports: [
        ReactiveFormsModule,
        RouterTestingModule.withRoutes([]),
        RegisterComponent
      ],
      providers: [
        { provide: AuthService, useValue: authServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService) as jasmine.SpyObj<AuthService>;
    router = TestBed.inject(Router);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have an invalid form when empty', () => {
    expect(component.registerForm.valid).toBeFalsy();
  });

  it('should call authService.register on submit with valid form', () => {
    component.registerForm.controls['firstName'].setValue('Test');
    component.registerForm.controls['lastName'].setValue('User');
    component.registerForm.controls['email'].setValue('test@example.com');
    component.registerForm.controls['password'].setValue('password');
    authService.register.and.returnValue(of({}));
    component.onSubmit();
    expect(authService.register).toHaveBeenCalledWith({
      firstName: 'Test',
      lastName: 'User',
      email: 'test@example.com',
      password: 'password'
    });
  });

  it('should navigate to /login on successful registration', () => {
    component.registerForm.controls['firstName'].setValue('Test');
    component.registerForm.controls['lastName'].setValue('User');
    component.registerForm.controls['email'].setValue('test@example.com');
    component.registerForm.controls['password'].setValue('password');
    authService.register.and.returnValue(of({}));
    spyOn(router, 'navigate');
    component.onSubmit();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should set error message on failed registration', () => {
    component.registerForm.controls['firstName'].setValue('Test');
    component.registerForm.controls['lastName'].setValue('User');
    component.registerForm.controls['email'].setValue('test@example.com');
    component.registerForm.controls['password'].setValue('password');
    authService.register.and.returnValue(throwError(() => new Error('Failed registration')));
    component.onSubmit();
    expect(component.error).toBe('Failed to register.');
  });
});