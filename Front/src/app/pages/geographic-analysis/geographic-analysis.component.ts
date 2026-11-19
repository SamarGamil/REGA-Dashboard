import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxEchartsDirective } from 'ngx-echarts';
import { Subscription } from 'rxjs';
import { LayoutComponent } from '../../components/feature/layout/layout.component';
import { ChartCardComponent } from '../../components/base/chart-card/chart-card.component';
import { ChartEmptyStateComponent } from '../../components/base/chart-empty-state/chart-empty-state.component';
import { DashboardApiService } from '../../services/dashboard-api.service';
import { FilterStateService } from '../../services/filter-state.service';
import { formatCount, formatCurrencyShort } from '../../utils/format-number';
import { applyRtl, baseChartTheme, CHART_COLORS } from '../../utils/chart-theme';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-geographic-analysis',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, LayoutComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './geographic-analysis.component.html',
})
export class GeographicAnalysisComponent implements OnInit, OnDestroy {
  mode: 'count' | 'value' = 'count';
  regions: any[] = [];
  hasData = false;
  regionalDetailsOptions: EChartsOption = {};

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
    this.hasData = false;
    this.api.getRegions(this.filterState.toQueryParams()).subscribe(data => {
      this.hasData = data.regions?.length > 0;
      this.regions = data.regions;
      this.buildCharts(data.regions);
    });
  }

  get maxTx(): number { return Math.max(...this.regions.map(r => r.transactions)); }
  get maxVal(): number { return Math.max(...this.regions.map(r => r.value)); }

  get sortedRegions() {
    return [...this.regions].sort((a, b) => this.mode === 'count' ? b.transactions - a.transactions : b.value - a.value);
  }

  // The region-details chart is a 100%-stacked operation-type breakdown; a transaction-type
  // filter collapses every region to 100% of that type, so the chart is uninformative then.
  get typeFilterActive(): boolean {
    return !!this.filterState.transactionType;
  }

  getHeatRatio(region: any): number {
    const val = this.mode === 'count' ? region.transactions : region.value;
    const max = this.mode === 'count' ? this.maxTx : this.maxVal;
    return val / max;
  }

  getHeatColor(region: any): string {
    const ratio = this.getHeatRatio(region);
    if (ratio > 0.8) return 'bg-[#002F56]';
    if (ratio > 0.6) return 'bg-[#1E5488]';
    if (ratio > 0.4) return 'bg-[#4F86BE]';
    if (ratio > 0.2) return 'bg-[#A9CBE6]';
    return 'bg-[#DCEAF5]';
  }

  getDisplayVal(region: any): string {
    return this.mode === 'count' ? formatCount(region.transactions) : formatCurrencyShort(region.value);
  }

  getBarWidth(region: any): number {
    return this.mode === 'count' ? (region.transactions / this.maxTx) * 100 : (region.value / this.maxVal) * 100;
  }

  private buildCharts(regions: any[]) {
    this.regionalDetailsOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: {
        trigger: 'axis',
        backgroundColor: CHART_COLORS.tooltipBg,
        borderWidth: 0,
        textStyle: { color: '#fff' },
        // Values are shares of each transaction type per region; suffix % so they aren't read as counts.
        formatter: (params: any) => {
          const items = Array.isArray(params) ? params : [params];
          const title = items[0]?.axisValueLabel ?? '';
          const lines = items.map((p: any) => `${p.marker}${p.seriesName}: ${p.value ?? 0}%`);
          return [title, ...lines].join('<br/>');
        },
      },
      legend: { bottom: 0, itemWidth: 10, itemHeight: 10, itemGap: 12, textStyle: { fontSize: 11, color: CHART_COLORS.axis } },
      grid: { top: 20, right: 20, bottom: 48, left: 130 },
      xAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: '{value}%' },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      yAxis: { 
        type: 'category', 
        data: regions.map(r => r.name),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis }
      },
      series: [
        { name: 'شراء', type: 'bar', stack: 'total', data: regions.map(r => r.purchasePct), itemStyle: { color: CHART_COLORS.primary }, barWidth: 16 },
        { name: 'بيع', type: 'bar', stack: 'total', data: regions.map(r => r.salePct), itemStyle: { color: CHART_COLORS.accent } },
        { name: 'هبة', type: 'bar', stack: 'total', data: regions.map(r => r.giftPct), itemStyle: { color: CHART_COLORS.positive } },
      ],
    });
  }
}
