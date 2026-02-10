import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ImportJob, ColumnMapping, PreviewData } from '../models/import';

@Injectable({
    providedIn: 'root'
})
export class ImportService {
    private apiUrl = '/api/import';

    constructor(private http: HttpClient) { }

    uploadFile(file: File): Observable<PreviewData> {
        const formData = new FormData();
        formData.append('file', file);
        return this.http.post<PreviewData>(`${this.apiUrl}/upload`, formData);
    }

    processFile(fileName: string, mapping: ColumnMapping): Observable<ImportJob> {
        return this.http.post<ImportJob>(`${this.apiUrl}/process`, { fileName, mapping });
    }

    getHistory(): Observable<ImportJob[]> {
        return this.http.get<ImportJob[]>(`${this.apiUrl}/history`);
    }
}
