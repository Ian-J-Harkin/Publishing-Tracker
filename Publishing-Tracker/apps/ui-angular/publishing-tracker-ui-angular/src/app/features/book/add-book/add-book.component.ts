import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { BookService } from '../../../core/services/book.service';
import { CreateBook } from '../../../core/models/book';

@Component({
  selector: 'app-add-book',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './add-book.component.html',
  styleUrls: ['./add-book.component.css']
})
export class AddBookComponent {
  addBookForm: FormGroup;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private bookService: BookService,
    private router: Router
  ) {
    this.addBookForm = this.fb.group({
      title: ['', Validators.required],
      author: ['', Validators.required],
      isbn: [''],
      publicationDate: [''],
      basePrice: [null],
      genre: [''],
      description: ['']
    });
  }

  onSubmit(): void {
    if (this.addBookForm.valid) {
      const newBook: CreateBook = this.addBookForm.value;
      this.bookService.createBook(newBook).subscribe({
        next: () => {
          this.router.navigate(['/books']);
        },
        error: (err) => {
          this.error = 'Failed to create book.';
        }
      });
    }
  }
}