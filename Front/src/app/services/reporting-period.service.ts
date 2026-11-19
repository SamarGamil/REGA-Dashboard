import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { ProductService } from './product.service';

/// Current reporting period (latest quarter) per product, fetched once and cached.
@Injectable({ providedIn: 'root' })
export class ReportingPeriodService {
  private http = inject(HttpClient);
  private product = inject(ProductService);
  private cache = signal<Record<string, { period: string; lastUpdated: string }>>({});

  readonly current = computed(() => this.cache()[this.product.activeProductId()]?.period ?? '');
  readonly currentLastUpdated = computed(() => this.cache()[this.product.activeProductId()]?.lastUpdated ?? '');

  constructor() {
    // Load reactively: SPA navigation updates the active product AFTER the page's
    // layout is created, so a constructor-time load can miss the just-entered product.
    effect(() => this.ensureLoaded());
  }

  ensureLoaded(): void {
    const id = this.product.activeProductId();
    if (this.cache()[id] !== undefined) return;
    const url =
      id === 'rental-indicators'
        ? `${environment.apiUrl}/rental-controls/reporting-period`
        : `${environment.apiUrl}/dashboard/reporting-period`;
    this.cache.update((c) => ({ ...c, [id]: { period: '', lastUpdated: '' } })); // placeholder guards against duplicate requests
    this.http
      .get<{ period: string; lastUpdated?: string }>(url)
      .subscribe((r) => this.cache.update((c) => ({ ...c, [id]: { period: r.period ?? '', lastUpdated: r.lastUpdated ?? '' } })));
  }
}
