import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
  selector: 'app-nationality-analysis',
  standalone: true,
  imports: [CommonModule, FormsModule, NgxEchartsDirective, LayoutComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './nationality-analysis.component.html',
})
export class NationalityAnalysisComponent implements OnInit, OnDestroy {
  formatCount = formatCount;
  search = '';
  nationalities: any[] = [];
  top10Nationalities: any[] = [];

  hasData = false;
  hasValueData = false;
  txChartOptions: EChartsOption = {};
  valueChartOptions: EChartsOption = {};
  avgValueOptions: EChartsOption = {};

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
    this.api.getNationalities(this.filterState.toQueryParams()).subscribe(data => {
      this.hasData = data.nationalities?.length > 0;
      // Show the value charts whenever ANY transaction is priced; gifts are unpriced so a gift-only period has none.
      this.hasValueData = (data.nationalities ?? []).some((n: any) => n.value > 0);
      this.nationalities = data.nationalities;
      this.top10Nationalities = [...data.nationalities].sort((a, b) => b.marketShare - a.marketShare).slice(0, 10);
      this.buildCharts(data.nationalities);
    });
  }

  get filtered() {
    return this.nationalities.filter(n => this.search === '' || n.name.includes(this.search));
  }

  // Compact SAR label (K/M/B) so both small (sub-million) and large values stay legible.
  private fmtValue(v: number): string {
    return formatCompact(v);
  }

  private buildCharts(nationalities: any[]) {
    const top10ByTx = [...nationalities].sort((a, b) => b.transactions - a.transactions).slice(0, 10).sort((a, b) => a.transactions - b.transactions);
    this.txChartOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      grid: { top: 10, right: 20, bottom: 20, left: 170 },
      xAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      yAxis: { 
        type: 'category', 
        data: top10ByTx.map(n => n.name),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, width: 160, overflow: 'break' }
      },
      series: [{ name: 'عدد العمليات', type: 'bar', data: top10ByTx.map(n => n.transactions), itemStyle: { color: CHART_COLORS.primary, borderRadius: [0, 4, 4, 0] }, barWidth: 20 }],
    });

    // Only nationalities with a real (non-zero) transaction value; gifts have no price so a
    // zero-value bar would render invisibly and make the chart look like it is missing rows.
    const top10ByValue = [...nationalities].filter(n => n.value > 0).sort((a, b) => b.value - a.value).slice(0, 10).sort((a, b) => a.value - b.value);
    this.valueChartOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => { const p = Array.isArray(params) ? params[0] : params; return `${p.name}<br/>${this.fmtValue(p.value)} ر.س`; } },
      grid: { top: 10, right: 20, bottom: 20, left: 170 },
      xAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: (v: number) => this.fmtValue(v) },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      yAxis: { 
        type: 'category', 
        data: top10ByValue.map(n => n.name),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, width: 160, overflow: 'break' }
      },
      series: [{ name: 'قيمة الصفقات', type: 'bar', data: top10ByValue.map(n => n.value), itemStyle: { color: CHART_COLORS.accent, borderRadius: [0, 4, 4, 0] }, barWidth: 20 }],
    });

    this.avgValueOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => { const p = Array.isArray(params) ? params[0] : params; return `${p.name}<br/>${this.fmtValue(p.value)} ر.س`; } },
      grid: { top: 20, right: 20, bottom: 80, left: 60 },
      xAxis: { 
        type: 'category', 
        data: nationalities.map(n => n.name),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 45, fontSize: 10, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: (v: number) => this.fmtValue(v) },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } },
        name: 'ر.س', nameLocation: 'middle', nameGap: 45, nameTextStyle: { fontSize: 11, color: CHART_COLORS.axis }
      },
      series: [{ name: 'متوسط قيمة الصفقة', type: 'bar', data: nationalities.map(n => n.avgValue), itemStyle: { color: CHART_COLORS.positive, borderRadius: [4, 4, 0, 0] }, barWidth: 32 }],
    });
  }
}
