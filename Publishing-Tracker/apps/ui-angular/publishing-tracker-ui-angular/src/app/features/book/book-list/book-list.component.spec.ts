import { render, screen } from '@testing-library/angular';
import { BookListComponent } from './book-list.component';
import { BookService } from '../../../core/services/book.service';
import { of } from 'rxjs';
import { RouterTestingModule } from '@angular/router/testing';

describe('BookListComponent', () => {
  it('should render the book list page', async () => {
    await render(BookListComponent, {
      imports: [RouterTestingModule],
      providers: [
        {
          provide: BookService,
          useValue: { getBooks: () => of([]) },
        },
      ],
    });

    expect(screen.getByRole('heading', { name: /ip portfolio/i })).toBeTruthy();
  });
});