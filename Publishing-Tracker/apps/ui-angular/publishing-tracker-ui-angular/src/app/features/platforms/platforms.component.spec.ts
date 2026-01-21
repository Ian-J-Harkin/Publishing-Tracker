import { render, screen } from '@testing-library/angular';
import { PlatformsComponent } from './platforms.component';
import { PlatformService } from '../../core/services/platform.service';
import { of } from 'rxjs';
import { RouterTestingModule } from '@angular/router/testing';
import { Platform } from '../../core/models/platform';

describe('PlatformsComponent', () => {
  it('should render platforms', async () => {
    const platforms: Platform[] = [{ id: 1, name: 'Platform 1', baseUrl: 'url1', commissionRate: 0.1 }];
    await render(PlatformsComponent, {
      imports: [RouterTestingModule],
      providers: [
        {
          provide: PlatformService,
          useValue: { getPlatforms: () => of(platforms) },
        },
      ],
    });

    expect(screen.getByText('Platform 1 - url1 - 0.1')).toBeTruthy();
  });
});