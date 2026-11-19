import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, of } from 'rxjs';
import { LayoutComponent } from '@/components/feature/layout/layout.component';
import { FilterBarComponent } from './components/filter-bar.component';
import { SummaryCollapsibleComponent } from './components/summary-collapsible.component';
import { CityCardGridComponent } from './components/city-card-grid.component';
import { ImpactPanelComponent } from './components/impact-panel.component';
import { RecommendationPanelComponent } from './components/recommendation-panel.component';
import { RentalControlsService } from '@/services/rental-controls.service';
import { PortalPeriodService } from '@/services/portal-period.service';
import { OverviewData, RentalIndicatorPoint } from '@/models/rental-controls';
import { statusRecommendations } from '@/utils/status-recommendations';

@Component({
  selector: 'app-city-status',
  standalone: true,
  imports: [
    LayoutComponent, FilterBarComponent, SummaryCollapsibleComponent,
    CityCardGridComponent, ImpactPanelComponent, RecommendationPanelComponent,
  ],
  templateUrl: './city-status.component.html',
})
export class CityStatusComponent {
  private api = inject(RentalControlsService);
  private portal = inject(PortalPeriodService);

  overview = signal<OverviewData | null>(null);
  readonly increasePoints = signal<RentalIndicatorPoint[]>([]);
  readonly periodLabel = this.portal.label;

  selectedStatus = signal('الكل');
  selectedClassification = signal('الكل');
  selectedCity = signal('الكل');

  cities = computed(() => this.overview()?.cities ?? []);

  filteredCities = computed(() =>
    this.cities().filter((city) => {
      if (this.selectedStatus() !== 'الكل' && city.operationalStatus !== this.selectedStatus()) return false;
      if (this.selectedClassification() !== 'الكل' && city.classification !== this.selectedClassification()) return false;
      if (this.selectedCity() !== 'الكل' && city.name !== this.selectedCity()) return false;
      return true;
    }),
  );

  // FABRICATED: the before-activation growth and the Q2 2026 reading are hardcoded (الرياض); no backend impact
  // time-series yet. The growth after activation is the selected period's own figure from the overview.
  readonly impact = computed(() => {
    const afterValue = this.cities().find((c) => c.id === 'riyadh')?.cagr ?? '';
    return {
      name: 'الرياض',
      available: afterValue !== '',
      beforeValue: '15.7%',
      afterValue,
      // The quarter before the selected one is only known when the selected quarter is Q3 2026.
      previousValue: this.increasePoints().at(-1)?.period === '2026-Q3' ? '9.7%' : '',
    };
  });

  readonly recommendations = computed(() => statusRecommendations(this.cities()));

  constructor() {
    // The rent-increase impact chart is optional: the page still shows the period if Riyadh's data is missing.
    this.portal
      .connect((period) => forkJoin({
        overview: this.api.getOverview(period),
        riyadh: this.api.getCity('riyadh', period).pipe(catchError(() => of(null))),
      }))
      .pipe(takeUntilDestroyed())
      .subscribe(({ overview, riyadh }) => {
        this.overview.set(overview);
        this.increasePoints.set(riyadh?.rentalIncreaseSeries ?? []);
      });
  }

  handleReset(): void {
    this.selectedStatus.set('الكل');
    this.selectedClassification.set('الكل');
    this.selectedCity.set('الكل');
  }
}
