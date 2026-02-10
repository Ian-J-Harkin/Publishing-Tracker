import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Sale, CreateSale, SalesSummary, SalesFilter } from '../models/sale';

@Injectable({
    providedIn: 'root'
})
export class SaleService {
    private apiUrl = '/api/sales';

    constructor(private http: HttpClient) { }

    getSales(filters: SalesFilter = {}): Observable<Sale[]> {
        const params = this.buildParams(filters);
        return this.http.get<Sale[]>(this.apiUrl, { params });
    }

    getSummary(filters: Partial<SalesFilter> = {}): Observable<SalesSummary> {
        const params = this.buildParams(filters);
        return this.http.get<SalesSummary>(`${this.apiUrl}/summary`, { params });
    }

    createSale(sale: CreateSale): Observable<Sale> {
        return this.http.post<Sale>(this.apiUrl, sale);
    }

    private buildParams(filters: Partial<SalesFilter>): HttpParams {
        let params = new HttpParams();
        for (const [key, value] of Object.entries(filters)) {
            if (value !== undefined && value !== null && value !== '') {
                params = params.set(key, value.toString());
            }
        }
        return params;
    }
}
