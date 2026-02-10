import { render, screen } from '@testing-library/angular';
import { BookPerformanceComponent } from './book-performance.component';
import { BookService } from '../../../core/services/book.service';
import { of } from 'rxjs';
import { RouterTestingModule } from '@angular/router/testing';
import { ActivatedRoute } from '@angular/router';

describe('BookPerformanceComponent', () => {
  it('should render book performance data', async () => {
    const performanceData = [{ platformName: 'Platform 1', totalRevenue: 100, currency: 'USD' }];
    const { getByText } = await render(BookPerformanceComponent, {
      imports: [RouterTestingModule],
      providers: [
        {
          provide: BookService,
          useValue: { getBookPerformance: () => of(performanceData) },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (id: string) => '1',
              },
            },
          },
        },
      ],
    });

    expect(getByText(/Platform 1:/)).toBeTruthy();
    expect(getByText('USD')).toBeTruthy();
    expect(getByText('100.00')).toBeTruthy();
  });

  it('should render an Export to CSV button', async () => {
    const performanceData = [{ platformName: 'Platform 1', totalRevenue: 100, currency: 'USD' }];
    const { getByText } = await render(BookPerformanceComponent, {
      imports: [RouterTestingModule],
      providers: [
        {
          provide: BookService,
          useValue: { getBookPerformance: () => of(performanceData) },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (id: string) => '1',
              },
            },
          },
        },
      ],
    });

    expect(getByText('Export to CSV')).toBeTruthy();
  });
});