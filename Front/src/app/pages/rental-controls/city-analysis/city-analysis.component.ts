import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, map, of, switchMap } from 'rxjs';
import { LayoutComponent } from '@/components/feature/layout/layout.component';
import { CityAnalysisHeaderComponent } from './components/city-analysis-header.component';
import { ExecutiveSummaryComponent } from './components/executive-summary.component';
import { MediaMonitoringComponent } from './components/media-monitoring.component';
import { KpiCardsComponent } from './components/kpi-cards.component';
import { MarketMovementComponent } from './components/market-movement.component';
import { RentalIndicatorChartsComponent } from './components/rental-indicator-charts.component';
import { MarketAnalysisTabsComponent } from './components/market-analysis-tabs.component';
import { ExecutiveRecommendationsComponent } from './components/executive-recommendations.component';
import { WhatToMonitorComponent } from './components/what-to-monitor.component';
import { CityJourneyComponent } from './components/city-journey.component';
import { PeriodFilterComponent } from '@/components/feature/period-filter/period-filter.component';
import { RentalControlsService } from '@/services/rental-controls.service';
import { PortalPeriodService } from '@/services/portal-period.service';
import { CityAnalysisData, CityListItem } from '@/models/rental-controls';

@Component({
  selector: 'app-rc-city-analysis',
  standalone: true,
  imports: [
    LayoutComponent, CityAnalysisHeaderComponent, ExecutiveSummaryComponent, MediaMonitoringComponent,
    KpiCardsComponent, MarketMovementComponent, RentalIndicatorChartsComponent, MarketAnalysisTabsComponent,
    ExecutiveRecommendationsComponent, WhatToMonitorComponent, CityJourneyComponent, PeriodFilterComponent,
  ],
  templateUrl: './city-analysis.component.html',
})
export class CityAnalysisComponent {
  private api = inject(RentalControlsService);
  private route = inject(ActivatedRoute);
  private portal = inject(PortalPeriodService);

  // The city to show: ?city= first, then the user's choice. It is kept across periods that list it; a city change and a
  // period change run through the same load, so the newer one always wins.
  private readonly city$ = new BehaviorSubject<string | null>(this.route.snapshot.queryParamMap.get('city'));

  cities = signal<CityListItem[]>([]);
  data = signal<CityAnalysisData | null>(null);
  readonly loading = this.portal.loading.asReadonly();

  constructor() {
    this.portal
      .connect((period) => this.api.getCities(period).pipe(
        switchMap((list) => {
          const wanted = this.city$.value;
          const id = wanted && list.some((c) => c.cityId === wanted) ? wanted : list[0]?.cityId ?? null;
          return id
            ? this.api.getCity(id, period).pipe(map((city) => ({ list, city })))
            : of({ list, city: null });
        }),
      ), this.city$)
      .pipe(takeUntilDestroyed())
      .subscribe(({ list, city }) => {
        this.cities.set(list);
        this.data.set(city);
      });
  }

  handleCityChange(cityId: string): void {
    this.city$.next(cityId);
  }
}
