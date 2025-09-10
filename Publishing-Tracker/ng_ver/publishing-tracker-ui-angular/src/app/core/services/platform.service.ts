import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Platform, PlatformRequest } from '../models/platform';

@Injectable({
  providedIn: 'root'
})
export class PlatformService {
  private apiUrl = 'http://localhost:5000/api/platforms';

  constructor(private http: HttpClient) { }

  private getAuthHeaders(): HttpHeaders {
    const token = localStorage.getItem('token');
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  getPlatforms(): Observable<Platform[]> {
    return this.http.get<Platform[]>(this.apiUrl, { headers: this.getAuthHeaders() });
  }

  requestPlatform(platformRequest: PlatformRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/requests`, platformRequest, { headers: this.getAuthHeaders() });
  }
}