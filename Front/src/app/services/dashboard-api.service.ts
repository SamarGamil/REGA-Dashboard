import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getSummary(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/summary`, { params });
  }

  getRegions(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/regions`, { params });
  }

  getNationalities(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/nationalities`, { params });
  }

  getProperties(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/properties`, { params });
  }

  getAreas(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/areas`, { params });
  }

  getFinancial(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/financial`, { params });
  }

  getStrategic(params?: HttpParams): Observable<any> {
    return this.http.get(`${this.baseUrl}/dashboard/strategic`, { params });
  }
}
