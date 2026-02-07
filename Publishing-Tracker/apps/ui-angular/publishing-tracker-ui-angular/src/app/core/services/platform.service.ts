import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Platform, PlatformRequest } from '../models/platform';

@Injectable({
  providedIn: 'root'
})
export class PlatformService {
  private apiUrl = '/api/platforms';

  constructor(private http: HttpClient) { }

  getPlatforms(): Observable<Platform[]> {
    return this.http.get<Platform[]>(this.apiUrl);
  }

  requestPlatform(platformRequest: PlatformRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/requests`, platformRequest);
  }
}