import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardSummary } from '../../core/models/dashboard';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  summary$!: Observable<DashboardSummary>;
  yoy$!: Observable<any>;
  seasonal$!: Observable<any>;
  error: string | null = null;

  constructor(
    private dashboardService: DashboardService,
    private authService: AuthService
  ) { }

  ngOnInit(): void {
    this.summary$ = this.dashboardService.getDashboardSummary();
    this.yoy$ = this.dashboardService.getYoYComparison();
    this.seasonal$ = this.dashboardService.getSeasonalPerformance();
  }

  logout(): void {
    this.authService.logout();
  }
}