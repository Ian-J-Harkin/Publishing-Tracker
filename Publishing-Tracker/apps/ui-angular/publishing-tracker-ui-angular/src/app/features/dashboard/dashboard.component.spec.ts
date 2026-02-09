import { render, screen } from '@testing-library/angular';
import { DashboardComponent } from './dashboard.component';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { of } from 'rxjs';
import { RouterTestingModule } from '@angular/router/testing';

describe('DashboardComponent', () => {
  it('should render dashboard data', async () => {
    const summary = {
      revenueByCurrency: [{ currency: 'USD', totalAmount: 1000 }],
      totalBooksPublished: 10,
      totalSalesTransactions: 100,
      topPerformingBook: 'Book 1',
      topPerformingPlatform: 'Platform 1',
    };
    const yoy = { currentYearRevenue: 1000, previousYearRevenue: 800, growth: 0.25 };
    const seasonal = [{ month: 1, totalRevenue: 100 }];

    await render(DashboardComponent, {
      imports: [RouterTestingModule],
      providers: [
        {
          provide: DashboardService,
          useValue: {
            getDashboardSummary: () => of(summary),
            getYoYComparison: () => of(yoy),
            getSeasonalPerformance: () => of(seasonal),
          },
        },
        {
          provide: AuthService,
          useValue: { logout: () => { } },
        },
      ],
    });

    expect(screen.getByText('Cumulative Revenue')).toBeTruthy();
    expect(screen.getByText('USD')).toBeTruthy();
    expect(screen.getByText('1,000.00')).toBeTruthy();
  });
});