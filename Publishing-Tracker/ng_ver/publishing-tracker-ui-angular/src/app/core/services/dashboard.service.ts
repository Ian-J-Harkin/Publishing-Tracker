import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardSummary } from '../models/dashboard';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private apiUrl = import.meta.env.VITE_API_BASE_URL + '/api/dashboard';

  constructor(private http: HttpClient) { }

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  getDashboardSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${this.apiUrl}/summary`, { headers: this.getAuthHeaders() });
  }

  getYoYComparison(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/yoy`, { headers: this.getAuthHeaders() });
  }

  getSeasonalPerformance(): Observable<any> {
    return this.http.get<any>(`${this.apiUrl}/seasonal`, { headers: this.getAuthHeaders() });
  }
}