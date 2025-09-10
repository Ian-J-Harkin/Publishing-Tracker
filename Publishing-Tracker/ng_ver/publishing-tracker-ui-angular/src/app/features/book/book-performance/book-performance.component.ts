import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BookService } from '../../../core/services/book.service';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-book-performance',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './book-performance.component.html',
  styleUrls: ['./book-performance.component.css']
})
export class BookPerformanceComponent implements OnInit {
  performanceData$!: Observable<any[]>;
  error: string | null = null;

  constructor(
    private bookService: BookService,
    private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.performanceData$ = this.bookService.getBookPerformance(+id);
    }
  }
}