import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardSummary, YoYComparison, SeasonalPerformance } from '../models/dashboard';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private apiUrl = '/api/dashboard';

  constructor(private http: HttpClient) { }

  getDashboardSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.apiUrl}/summary`);
  }

  getYoYComparison(): Observable<YoYComparison> {
    return this.http.get<YoYComparison>(`${this.apiUrl}/yoy`);
  }

  getSeasonalPerformance(): Observable<SeasonalPerformance[]> {
    return this.http.get<SeasonalPerformance[]>(`${this.apiUrl}/seasonal`);
  }
}