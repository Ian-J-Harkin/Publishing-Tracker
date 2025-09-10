import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';

import { RequestPlatformComponent } from './request-platform.component';
import { PlatformService } from '../../../core/services/platform.service';

describe('RequestPlatformComponent', () => {
  let component: RequestPlatformComponent;
  let fixture: ComponentFixture<RequestPlatformComponent>;
  let platformService: jasmine.SpyObj<PlatformService>;

  beforeEach(async () => {
    const platformServiceSpy = jasmine.createSpyObj('PlatformService', ['requestPlatform']);
    platformServiceSpy.requestPlatform.and.returnValue(of(null));

    await TestBed.configureTestingModule({
      imports: [
        ReactiveFormsModule,
        RouterTestingModule,
        RequestPlatformComponent
      ],
      providers: [
        { provide: PlatformService, useValue: platformServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RequestPlatformComponent);
    component = fixture.componentInstance;
    platformService = TestBed.inject(PlatformService) as jasmine.SpyObj<PlatformService>;
    fixture.detectChanges();
  });

  it('should submit a new platform request', () => {
    component.requestPlatformForm.controls['name'].setValue('New Platform');
    component.requestPlatformForm.controls['baseUrl'].setValue('new-url');
    component.requestPlatformForm.controls['commissionRate'].setValue(0.2);

    component.onSubmit();

    expect(platformService.requestPlatform).toHaveBeenCalledWith({
      name: 'New Platform',
      baseUrl: 'new-url',
      commissionRate: 0.2
    });
  });
});