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
import { formatNumber, formatCurrencyShort, formatPercent, formatCompact, formatCount } from '../../utils/format-number';
import { applyRtl, baseChartTheme, CHART_COLORS } from '../../utils/chart-theme';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-executive-summary',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, LayoutComponent, KpiCardComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './executive-summary.component.html',
})
export class ExecutiveSummaryComponent implements OnInit, OnDestroy {
  kpi: any = {};
  formatNumber = formatNumber;
  formatCount = formatCount;
  formatCurrencyShort = formatCurrencyShort;
  formatPercent = formatPercent;

  hasData = false;
  volumeChartOptions: EChartsOption = {};
  valueChartOptions: EChartsOption = {};

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
    this.api.getSummary(this.filterState.toQueryParams()).subscribe(data => {
      this.hasData = data.monthlySummary?.length > 0;
      this.kpi = data.kpi;
      this.buildCharts(data.monthlySummary);
    });
  }

  private buildCharts(monthlySummary: any[]) {
    this.volumeChartOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      legend: { bottom: 0, itemWidth: 12, itemHeight: 8, itemGap: 12, textStyle: { fontSize: 11, color: CHART_COLORS.axis } },
      grid: { top: 30, right: 20, bottom: 44, left: 50 },
      xAxis: { 
        type: 'category', 
        data: monthlySummary.map(m => m.label),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, hideOverlap: true, interval: 'auto' }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [
        { name: 'إجمالي العمليات', type: 'bar', data: monthlySummary.map(m => m.total), itemStyle: { color: CHART_COLORS.primary, borderRadius: [4, 4, 0, 0] }, barWidth: 32 },
        { name: 'عمليات الشراء', type: 'line', data: monthlySummary.map(m => m.purchases), itemStyle: { color: CHART_COLORS.positive }, lineStyle: { width: 2 }, smooth: true },
      ],
    });

    this.valueChartOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => {
        const p = Array.isArray(params) ? params[0] : params;
        return `${p.name}<br/>${formatCompact(p.value)} ر.س`;
      }},
      grid: { top: 20, right: 20, bottom: 30, left: 60 },
      xAxis: { 
        type: 'category', 
        data: monthlySummary.map(m => m.label),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, hideOverlap: true, interval: 'auto' }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: (v: number) => formatCompact(v) },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{
        name: 'قيمة الصفقات', type: 'line', data: monthlySummary.map(m => m.value),
        areaStyle: { color: { type: 'linear', x: 0, y: 0, x2: 0, y2: 1, colorStops: [{ offset: 0, color: 'rgba(0,132,141,0.3)' }, { offset: 1, color: 'rgba(0,132,141,0.05)' }] } },
        itemStyle: { color: CHART_COLORS.primary }, lineStyle: { width: 2 }, smooth: true,
      }],
    });
  }
}
