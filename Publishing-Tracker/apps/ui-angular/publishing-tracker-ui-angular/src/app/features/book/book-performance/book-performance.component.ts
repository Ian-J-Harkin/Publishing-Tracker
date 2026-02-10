import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BookService } from '../../../core/services/book.service';
import { BookPerformance } from '../../../core/models/book';
import { Observable, tap } from 'rxjs';

@Component({
  selector: 'app-book-performance',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './book-performance.component.html',
  styleUrls: ['./book-performance.component.css']
})
export class BookPerformanceComponent implements OnInit {
  performanceData$!: Observable<BookPerformance[]>;
  private performanceData: BookPerformance[] = [];
  error: string | null = null;

  constructor(
    private bookService: BookService,
    private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.performanceData$ = this.bookService.getBookPerformance(+id).pipe(
        tap(data => this.performanceData = data)
      );
    }
  }

  exportToCsv(): void {
    if (this.performanceData.length === 0) return;

    const header = 'Platform Name,Currency,Total Revenue';
    const rows = this.performanceData.map(
      p => `${p.platformName},${p.currency},${p.totalRevenue.toFixed(2)}`
    );
    const csvContent = [header, ...rows].join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'book-performance.csv';
    link.click();
    URL.revokeObjectURL(url);
  }
}