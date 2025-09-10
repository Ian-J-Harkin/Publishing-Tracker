import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Book, CreateBook, UpdateBook } from '../models/book';

@Injectable({
  providedIn: 'root'
})
export class BookService {
  private apiUrl = 'http://localhost:5000/api/books';

  constructor(private http: HttpClient) { }

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  getBooks(): Observable<Book[]> {
    return this.http.get<Book[]>(this.apiUrl, { headers: this.getAuthHeaders() });
  }

  getBook(id: number): Observable<Book> {
    return this.http.get<Book>(`${this.apiUrl}/${id}`, { headers: this.getAuthHeaders() });
  }

  createBook(book: CreateBook): Observable<Book> {
    return this.http.post<Book>(this.apiUrl, book, { headers: this.getAuthHeaders() });
  }

  updateBook(id: number, book: UpdateBook): Observable<Book> {
    return this.http.put<Book>(`${this.apiUrl}/${id}`, book, { headers: this.getAuthHeaders() });
  }

  deleteBook(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`, { headers: this.getAuthHeaders() });
  }

  getBookPerformance(id: number): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/${id}/performance`, { headers: this.getAuthHeaders() });
  }
}