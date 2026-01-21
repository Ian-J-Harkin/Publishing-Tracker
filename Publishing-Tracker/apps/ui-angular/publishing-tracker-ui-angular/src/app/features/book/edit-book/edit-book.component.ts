import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { BookService } from '../../../core/services/book.service';
import { Book, UpdateBook } from '../../../core/models/book';

@Component({
  selector: 'app-edit-book',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './edit-book.component.html',
  styleUrls: ['./edit-book.component.css']
})
export class EditBookComponent implements OnInit {
  editBookForm: FormGroup;
  book!: Book;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private bookService: BookService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.editBookForm = this.fb.group({
      title: ['', Validators.required],
      author: ['', Validators.required],
      isbn: [''],
      publicationDate: [''],
      basePrice: [null],
      genre: [''],
      description: ['']
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.bookService.getBook(+id).subscribe(book => {
        this.book = book;
        this.editBookForm.patchValue(book);
      });
    }
  }

  onSubmit(): void {
    if (this.editBookForm.valid) {
      const updatedBook: UpdateBook = this.editBookForm.value;
      this.bookService.updateBook(this.book.id, updatedBook).subscribe({
        next: () => {
          this.router.navigate(['/books']);
        },
        error: (err) => {
          this.error = 'Failed to update book.';
        }
      });
    }
  }
}