import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BookService } from '../../../core/services/book.service';
import { Book } from '../../../core/models/book';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-book-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './book-list.component.html',
  styleUrls: ['./book-list.component.css']
})
export class BookListComponent implements OnInit {
  books$!: Observable<Book[]>;
  error: string | null = null;

  constructor(private bookService: BookService) { }

  ngOnInit(): void {
    this.books$ = this.bookService.getBooks();
  }

  deleteBook(id: number): void {
    if (window.confirm('Are you sure you want to delete this book?')) {
      this.bookService.deleteBook(id).subscribe({
        next: () => {
          this.books$ = this.bookService.getBooks();
        },
        error: (err) => {
          this.error = 'Failed to delete book.';
        }
      });
    }
  }
}
