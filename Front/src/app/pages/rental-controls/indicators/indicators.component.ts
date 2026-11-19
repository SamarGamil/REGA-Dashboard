import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { LayoutComponent } from '@/components/feature/layout/layout.component';
import { CurrentComparisonComponent } from './components/current-comparison.component';
import { SupplyDemandGapComponent } from './components/supply-demand-gap.component';
import { HistoricalComparisonComponent } from './components/historical-comparison.component';
import { RentalControlsService } from '@/services/rental-controls.service';
import { PortalPeriodService } from '@/services/portal-period.service';
import { PeriodFilterComponent } from '@/components/feature/period-filter/period-filter.component';
import { IndicatorsData } from '@/models/rental-controls';

@Component({
  selector: 'app-rc-indicators',
  standalone: true,
  imports: [LayoutComponent, PeriodFilterComponent, CurrentComparisonComponent, SupplyDemandGapComponent, HistoricalComparisonComponent],
  templateUrl: './indicators.component.html',
})
export class IndicatorsComponent {
  private api = inject(RentalControlsService);
  private portal = inject(PortalPeriodService);

  indicators = signal<IndicatorsData | null>(null);
  activeCurrentId = signal<string | null>(null);
  historicalInitId = signal<string | undefined>(undefined);

  constructor() {
    this.portal
      .connect((period) => this.api.getIndicators(period))
      .pipe(takeUntilDestroyed())
      .subscribe((data) => this.indicators.set(data));
  }

  handleCurrentIndicatorClick(indicatorId: string): void {
    this.activeCurrentId.set(indicatorId);
    const mapped = this.indicators()?.currentToHistoricalMap[indicatorId];
    if (mapped) this.historicalInitId.set(mapped);
  }
}
