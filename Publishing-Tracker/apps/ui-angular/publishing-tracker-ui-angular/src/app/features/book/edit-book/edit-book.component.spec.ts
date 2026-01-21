import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';

import { EditBookComponent } from './edit-book.component';
import { BookService } from '../../../core/services/book.service';
import { Book } from '../../../core/models/book';

describe('EditBookComponent', () => {
  let component: EditBookComponent;
  let fixture: ComponentFixture<EditBookComponent>;
  let bookService: jasmine.SpyObj<BookService>;
  let router: Router;
  let route: ActivatedRoute;

  const mockBook: Book = {
    id: 1,
    title: 'Test Book',
    author: 'Test Author',
    isbn: '1234567890',
    publicationDate: new Date(),
    basePrice: 19.99,
    genre: 'Fiction',
    description: 'A test book.'
  };

  beforeEach(async () => {
    const bookServiceSpy = jasmine.createSpyObj('BookService', ['getBook', 'updateBook']);
    bookServiceSpy.getBook.and.returnValue(of(mockBook));
    bookServiceSpy.updateBook.and.returnValue(of(mockBook));

    await TestBed.configureTestingModule({
      imports: [
        ReactiveFormsModule,
        RouterTestingModule.withRoutes([]),
        EditBookComponent
      ],
      providers: [
        { provide: BookService, useValue: bookServiceSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => '1'
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(EditBookComponent);
    component = fixture.componentInstance;
    bookService = TestBed.inject(BookService) as jasmine.SpyObj<BookService>;
    router = TestBed.inject(Router);
    route = TestBed.inject(ActivatedRoute);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load the book data on init', () => {
    expect(component.book).toEqual(mockBook);
    expect(component.editBookForm.value.title).toEqual(mockBook.title);
  });

  it('should call updateBook on form submit', () => {
    component.editBookForm.patchValue({
      title: 'Updated Title',
      author: 'Updated Author'
    });

    component.onSubmit();

    expect(bookService.updateBook).toHaveBeenCalledWith(mockBook.id, jasmine.any(Object));
  });
});