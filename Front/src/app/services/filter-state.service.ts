import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Subject, Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface GeographyNode {
  name: string;
  children: GeographyNode[];
}

export interface FilterOptions {
  geography: GeographyNode[];
  nationalities: string[];
  propertyTypes: string[];
  transactionTypes: string[];
  ownerTypes: string[];
  minDate: string;
  maxDate: string;
}

@Injectable({ providedIn: 'root' })
export class FilterStateService {
  // Current selections
  region: string | null = null;
  city: string | null = null;
  district: string | null = null;
  nationalities: string[] = [];
  transactionType: string | null = null;
  ownerType: string | null = null;
  propertyType: string | null = null;
  period: 'monthly' | 'quarterly' | 'yearly' = 'monthly';

  // Options loaded from backend
  options: FilterOptions = {
    geography: [],
    nationalities: [],
    propertyTypes: [],
    transactionTypes: [],
    ownerTypes: [],
    minDate: '',
    maxDate: ''
  };

  filterChanged$ = new Subject<void>();

  constructor(private http: HttpClient) {}

  loadOptions(): Observable<FilterOptions> {
    return this.http.get<FilterOptions>(`${environment.apiUrl}/filters`);
  }

  getCities(): GeographyNode[] {
    if (!this.region) return [];
    const regionNode = this.options.geography.find(g => g.name === this.region);
    return regionNode?.children ?? [];
  }

  getDistricts(): GeographyNode[] {
    if (!this.city) return [];
    const cities = this.getCities();
    const cityNode = cities.find(c => c.name === this.city);
    return cityNode?.children ?? [];
  }

  toQueryParams(): HttpParams {
    let params = new HttpParams();
    if (this.region) params = params.set('region', this.region);
    if (this.city) params = params.set('city', this.city);
    if (this.district) params = params.set('district', this.district);
    if (this.nationalities.length > 0) params = params.set('nationalities', this.nationalities.join(','));
    if (this.transactionType) params = params.set('transactionType', this.transactionType);
    if (this.ownerType) params = params.set('ownerType', this.ownerType);
    if (this.propertyType) params = params.set('propertyType', this.propertyType);
    if (this.period) params = params.set('period', this.period);
    return params;
  }

  emitChange(): void {
    this.filterChanged$.next();
  }

  reset(): void {
    this.region = null;
    this.city = null;
    this.district = null;
    this.nationalities = [];
    this.transactionType = null;
    this.ownerType = null;
    this.propertyType = null;
    this.period = 'monthly';
    this.emitChange();
  }
}
