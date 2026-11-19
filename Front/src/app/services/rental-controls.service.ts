import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { OverviewData, IndicatorsData, CityListItem, CityAnalysisData, MarketAnalysisData } from '../models/rental-controls';

@Injectable({ providedIn: 'root' })
export class RentalControlsService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/rental-controls`;

  /** period: yyyy-MM | yyyy-Qn (GET /reports/periods); omitted = the live data. */
  getOverview(period?: string): Observable<OverviewData> {
    return this.http.get<OverviewData>(`${this.baseUrl}/overview`, { params: periodParams(period) });
  }

  getIndicators(period?: string): Observable<IndicatorsData> {
    return this.http.get<IndicatorsData>(`${this.baseUrl}/indicators`, { params: periodParams(period) });
  }

  getCities(period?: string): Observable<CityListItem[]> {
    return this.http.get<CityListItem[]>(`${this.baseUrl}/cities`, { params: periodParams(period) });
  }

  getCity(cityId: string, period?: string): Observable<CityAnalysisData> {
    return this.http.get<CityAnalysisData>(`${this.baseUrl}/cities/${cityId}`, { params: periodParams(period) });
  }

  getMarketAnalysis(): Observable<MarketAnalysisData> {
    return this.http.get<MarketAnalysisData>(`${this.baseUrl}/market-analysis`);
  }
}

// The API serves the live data when no period is given.
function periodParams(period?: string): Record<string, string> {
  return period ? { period } : {};
}
