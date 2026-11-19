import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxEchartsDirective } from 'ngx-echarts';
import { Subscription } from 'rxjs';
import { LayoutComponent } from '../../components/feature/layout/layout.component';
import { KpiCardComponent } from '../../components/base/kpi-card/kpi-card.component';
import { ChartCardComponent } from '../../components/base/chart-card/chart-card.component';
import { ChartEmptyStateComponent } from '../../components/base/chart-empty-state/chart-empty-state.component';
import { DashboardApiService } from '../../services/dashboard-api.service';
import { FilterStateService } from '../../services/filter-state.service';
import { formatCurrencyShort, formatCurrencyLong, formatCompact, formatCount } from '../../utils/format-number';
import { applyRtl, baseChartTheme, CHART_COLORS } from '../../utils/chart-theme';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-financial-analysis',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, LayoutComponent, KpiCardComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './financial-analysis.component.html',
})
export class FinancialAnalysisComponent implements OnInit, OnDestroy {
  fm: any = {};
  unit: 'full' | 'short' = 'full';

  hasData = false;
  valueTrendOptions: EChartsOption = {};
  growthBarOptions: EChartsOption = {};
  distributionOptions: EChartsOption = {};

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
    this.api.getFinancial(this.filterState.toQueryParams()).subscribe(data => {
      this.hasData = data.monthlySummary?.length > 0;
      this.fm = data.metrics;
      this.buildCharts(data.monthlySummary, data.growthRates, data.valueDistribution);
    });
  }

  getKpiValue(v: number): string {
    // Show an explicit 0 (not blank) when a filter yields no valued transactions.
    if (v == null) return '—';
    return this.unit === 'short' ? formatCurrencyLong(v) : formatCurrencyShort(v, true);
  }

  private buildCharts(monthlySummary: any[], growthRates: any[], valueDistribution: any[]) {
    this.valueTrendOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => { const p = Array.isArray(params) ? params[0] : params; return `${p.name}<br/>${formatCompact(p.value)} ر.س`; } },
      grid: { top: 20, right: 20, bottom: 60, left: 60 },
      xAxis: { 
        type: 'category', 
        data: monthlySummary.map(m => m.label),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 30, fontSize: 10, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: (v: number) => formatCompact(v) },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{
        type: 'line', data: monthlySummary.map(m => m.value), smooth: true,
        areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(0,132,141,0.35)' }, { offset: 1, color: 'rgba(0,132,141,0.05)' }] } },
        itemStyle: { color: CHART_COLORS.primary }, lineStyle: { width: 2 },
      }],
    });

    this.growthBarOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => { const p = Array.isArray(params) ? params[0] : params; return `${p.name}<br/>${p.value == null ? 'لا يوجد أساس للمقارنة' : p.value + '%'}`; } },
      grid: { top: 20, right: 20, bottom: 60, left: 50 },
      xAxis: { 
        type: 'category', 
        data: growthRates.map(g => g.label),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 30, fontSize: 10, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: '{value}%' },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{
        type: 'bar', barWidth: 42,
        data: growthRates.map(g => ({ value: g.rate, itemStyle: { color: g.rate == null ? CHART_COLORS.axis : (g.rate >= 0 ? CHART_COLORS.primary : '#B85450'), borderRadius: [4, 4, 0, 0] } })),
      }],
    });

    this.distributionOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      grid: { top: 20, right: 20, bottom: 50, left: 50 },
      xAxis: { 
        type: 'category', 
        data: valueDistribution.map(d => d.range),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 15, fontSize: 11, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{ type: 'bar', barWidth: 44, data: valueDistribution.map(d => d.count), itemStyle: { color: CHART_COLORS.primary, borderRadius: [4, 4, 0, 0] } }],
    });
  }
}
