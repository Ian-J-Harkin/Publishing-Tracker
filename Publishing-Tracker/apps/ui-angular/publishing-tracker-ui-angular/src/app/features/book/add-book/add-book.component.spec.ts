import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';

import { AddBookComponent } from './add-book.component';
import { BookService } from '../../../core/services/book.service';

describe('AddBookComponent', () => {
  let component: AddBookComponent;
  let fixture: ComponentFixture<AddBookComponent>;
  let bookService: jasmine.SpyObj<BookService>;

  beforeEach(async () => {
    const bookServiceSpy = jasmine.createSpyObj('BookService', ['createBook']);
    bookServiceSpy.createBook.and.returnValue(of({}));

    await TestBed.configureTestingModule({
      imports: [
        ReactiveFormsModule,
        RouterTestingModule,
        AddBookComponent
      ],
      providers: [
        { provide: BookService, useValue: bookServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AddBookComponent);
    component = fixture.componentInstance;
    bookService = TestBed.inject(BookService) as jasmine.SpyObj<BookService>;
    fixture.detectChanges();
  });

  it('should submit a new book', () => {
    component.addBookForm.controls['title'].setValue('New Book');
    component.addBookForm.controls['author'].setValue('New Author');
    component.addBookForm.controls['isbn'].setValue('1234567890');
    component.addBookForm.controls['publicationDate'].setValue(new Date('2023-01-01'));
    component.addBookForm.controls['basePrice'].setValue(19.99);
    component.addBookForm.controls['genre'].setValue('Fiction');
    component.addBookForm.controls['description'].setValue('A great book');

    component.onSubmit();

    expect(bookService.createBook).toHaveBeenCalledWith({
      title: 'New Book',
      author: 'New Author',
      isbn: '1234567890',
      publicationDate: new Date('2023-01-01'),
      basePrice: 19.99,
      genre: 'Fiction',
      description: 'A great book'
    });
  });
});