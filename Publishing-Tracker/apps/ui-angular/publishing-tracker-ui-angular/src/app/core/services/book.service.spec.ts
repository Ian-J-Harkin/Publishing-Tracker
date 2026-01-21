import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { BookService } from './book.service';
import { Book, CreateBook, UpdateBook } from '../models/book';

describe('BookService', () => {
  let service: BookService;
  let httpMock: HttpTestingController;
  const apiUrl = import.meta.env.VITE_API_BASE_URL + '/api/books';
  const mockToken = 'test-token';

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [BookService]
    });
    service = TestBed.inject(BookService);
    httpMock = TestBed.inject(HttpTestingController);
    spyOn(localStorage, 'getItem').and.returnValue(mockToken);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get books', () => {
    const mockBooks: Book[] = [{ id: 1, title: 'Test Book', author: 'Test Author', isbn: '123', publicationDate: new Date(), basePrice: 10, genre: 'Fiction', description: '' }];
    service.getBooks().subscribe(books => {
      expect(books).toEqual(mockBooks);
    });
    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockBooks);
  });

  it('should get a single book', () => {
    const mockBook: Book = { id: 1, title: 'Test Book', author: 'Test Author', isbn: '123', publicationDate: new Date(), basePrice: 10, genre: 'Fiction', description: '' };
    service.getBook(1).subscribe(book => {
      expect(book).toEqual(mockBook);
    });
    const req = httpMock.expectOne(`${apiUrl}/1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockBook);
  });

  it('should create a book', () => {
    const newBook: CreateBook = { title: 'New Book', author: 'New Author', isbn: '456', publicationDate: new Date(), basePrice: 20, genre: 'Non-Fiction', description: '' };
    const mockBook: Book = { id: 2, ...newBook };
    service.createBook(newBook).subscribe(book => {
      expect(book).toEqual(mockBook);
    });
    const req = httpMock.expectOne(apiUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(newBook);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockBook);
  });

  it('should update a book', () => {
    const updatedBook: UpdateBook = { title: 'Updated Book' };
    const mockBook: Book = { id: 1, title: 'Updated Book', author: 'Test Author', isbn: '123', publicationDate: new Date(), basePrice: 10, genre: 'Fiction', description: '' };
    service.updateBook(1, updatedBook).subscribe(book => {
      expect(book).toEqual(mockBook);
    });
    const req = httpMock.expectOne(`${apiUrl}/1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(updatedBook);
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockBook);
  });

  it('should delete a book', () => {
    service.deleteBook(1).subscribe();
    const req = httpMock.expectOne(`${apiUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush({});
  });

  it('should get book performance', () => {
    const mockPerformance = { sales: 100, revenue: 1000 };
    service.getBookPerformance(1).subscribe(performance => {
      expect(performance).toEqual(mockPerformance);
    });
    const req = httpMock.expectOne(`${apiUrl}/1/performance`);
    expect(req.request.method).toBe('GET');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${mockToken}`);
    req.flush(mockPerformance);
  });
});