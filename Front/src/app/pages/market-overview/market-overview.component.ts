import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxEchartsDirective } from 'ngx-echarts';
import { Subscription } from 'rxjs';
import { LayoutComponent } from '../../components/feature/layout/layout.component';
import { ChartCardComponent } from '../../components/base/chart-card/chart-card.component';
import { ChartEmptyStateComponent } from '../../components/base/chart-empty-state/chart-empty-state.component';
import { DashboardApiService } from '../../services/dashboard-api.service';
import { FilterStateService } from '../../services/filter-state.service';
import { applyRtl, baseChartTheme, CHART_COLORS } from '../../utils/chart-theme';
import { formatCompact, formatCount } from '../../utils/format-number';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-market-overview',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, LayoutComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './market-overview.component.html',
})
export class MarketOverviewComponent implements OnInit, OnDestroy {
  formatCount = formatCount;
  period = 'شهري';
  periods = ['يومي', 'أسبوعي', 'شهري', 'ربع سنوي', 'سنوي'];

  txTypeData = [
    { name: 'شراء', value: 759, color: CHART_COLORS.primary },
    { name: 'بيع', value: 32, color: CHART_COLORS.accent },
    { name: 'هبة', value: 233, color: CHART_COLORS.positive },
  ];

  hasData = false;
  hasTxTypeData = false;
  hasGrowthData = false;
  txOverTimeOptions: EChartsOption = {};
  valueOverTimeOptions: EChartsOption = {};
  donutOptions: EChartsOption = {};
  growthOptions: EChartsOption = {};

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
      const monthly = data.monthlySummary ?? [];
      this.hasData = monthly.length > 0;
      // Distribution reflects the current period (latest bucket), matching the KPI cards.
      const kpi = data.kpi ?? {};
      this.txTypeData = [
        { name: 'شراء', value: kpi.totalPurchases || 0, color: CHART_COLORS.primary },
        { name: 'بيع', value: kpi.totalSales || 0, color: CHART_COLORS.accent },
        { name: 'هبة', value: kpi.totalGifts || 0, color: CHART_COLORS.positive },
      ];
      this.hasTxTypeData = this.txTypeData.some(d => d.value > 0);
      this.buildCharts(data.monthlySummary, data.growthRates);
    });
  }

  private buildCharts(monthlySummary: any[], growthRates: any[]) {
    // Growth needs at least one period with a non-null rate (a prior baseline to compare against).
    this.hasGrowthData = (growthRates ?? []).some(g => g.rate != null);
    this.txOverTimeOptions = applyRtl({
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
        { name: 'شراء', type: 'bar', stack: 'total', data: monthlySummary.map(m => m.purchases), itemStyle: { color: CHART_COLORS.primary }, barWidth: 14 },
        { name: 'بيع', type: 'bar', stack: 'total', data: monthlySummary.map(m => m.sales), itemStyle: { color: CHART_COLORS.accent } },
        { name: 'هبة', type: 'bar', stack: 'total', data: monthlySummary.map(m => m.gifts), itemStyle: { color: CHART_COLORS.positive } },
      ],
    });

    this.valueOverTimeOptions = applyRtl({
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
      series: [{ name: 'القيمة', type: 'line', data: monthlySummary.map(m => m.value), itemStyle: { color: CHART_COLORS.primary }, lineStyle: { width: 2.5 }, smooth: true, symbol: 'circle', symbolSize: 8 }],
    });

    this.donutOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'item', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      series: [{
        type: 'pie', radius: ['50%', '75%'], padAngle: 3,
        data: this.txTypeData.map(d => ({ value: d.value, name: d.name, itemStyle: { color: d.color } })),
        label: { show: false },
      }],
    });

    this.growthOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => {
        const p = Array.isArray(params) ? params[0] : params;
        // A null rate means there's no prior period to compare against.
        return `${p.name}<br/>${p.value == null ? 'لا يوجد أساس للمقارنة' : p.value + '%'}`;
      }},
      xAxis: { 
        type: 'category', 
        data: growthRates.map(g => g.label),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, hideOverlap: true, interval: 'auto' }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: '{value}%' },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{
        name: 'معدل النمو', type: 'bar', barWidth: 40,
        data: growthRates.map(g => ({ value: g.rate, itemStyle: { color: g.rate == null ? CHART_COLORS.axis : (g.rate >= 0 ? CHART_COLORS.primary : '#B85450'), borderRadius: [4, 4, 0, 0] } })),
      }],
    });
  }
}
