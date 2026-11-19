import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { LayoutComponent } from '../../components/feature/layout/layout.component';
import { ChartCardComponent } from '../../components/base/chart-card/chart-card.component';
import { DashboardApiService } from '../../services/dashboard-api.service';
import { FilterStateService } from '../../services/filter-state.service';
import { formatCompact, formatCount } from '../../utils/format-number';

@Component({
  selector: 'app-strategic-insights',
  standalone: true,
  imports: [CommonModule, LayoutComponent, ChartCardComponent],
  templateUrl: './strategic-insights.component.html',
})
export class StrategicInsightsComponent implements OnInit, OnDestroy {
  formatCount = formatCount;
  data: any = {};
  circumference = 2 * Math.PI * 38;

  private sub!: Subscription;

  constructor(private api: DashboardApiService, private filterState: FilterStateService) {}

  ngOnInit() {
    this.loadData();
    this.sub = this.filterState.filterChanged$.subscribe(() => this.loadData());
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  private loadData() {
    this.api.getStrategic(this.filterState.toQueryParams()).subscribe(result => {
      this.data = result.insights;
    });
  }

  getOffset(value: number): number {
    return this.circumference - (value / 100) * this.circumference;
  }

  fmtValue(v: number): string {
    return formatCompact(v);
  }

  getConcentrationLabel(value: number): string {
    if (value >= 70) return 'تركّز عالٍ';
    if (value >= 40) return 'تركّز متوسط';
    return 'تركّز منخفض';
  }

  getDiversityLabel(value: number): string {
    if (value >= 70) return 'تنوع عالٍ';
    if (value >= 40) return 'تنوع متوسط';
    return 'تنوع محدود';
  }

  getSpreadLabel(value: number): string {
    if (value >= 70) return 'انتشار واسع';
    if (value >= 40) return 'انتشار متوسط';
    return 'انتشار محدود';
  }
}
