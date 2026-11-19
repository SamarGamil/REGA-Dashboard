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

const AREA_COLORS = [CHART_COLORS.primary, CHART_COLORS.accent, '#063A68', '#06641F', CHART_COLORS.positive, '#A2CD27'];
const AREA_KEYS = ['0–500', '501–1,000', '1,001–2,000', '2,001–5,000', '5,001–10,000', '10,000+'];
const AREA_LABELS = ['0–500 م²', '501–1,000 م²', '1,001–2,000 م²', '2,001–5,000 م²', '5,001–10,000 م²', '10,000+ م²'];

@Component({
  selector: 'app-area-analysis',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, LayoutComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './area-analysis.component.html',
})
export class AreaAnalysisComponent implements OnInit, OnDestroy {
  areaByNationality: any[] = [];
  areaKeys = AREA_KEYS;
  areaLabels = AREA_LABELS;
  areaColors = AREA_COLORS;
  formatCount = formatCount;

  hasData = false;
  txByAreaOptions: EChartsOption = {};
  avgByAreaOptions: EChartsOption = {};
  areaByRegionOptions: EChartsOption = {};

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
    this.api.getAreas(this.filterState.toQueryParams()).subscribe(data => {
      this.hasData = data.areaByNationality?.length > 0;
      this.areaByNationality = data.areaByNationality;
      this.buildCharts(data.areaSegments, data.areaByRegion);
    });
  }

  getVal(row: any, key: string): number {
    return row.segments?.[key] || 0;
  }

  getPct(row: any, key: string): number {
    const total = AREA_KEYS.reduce((acc, k) => acc + (row.segments?.[k] || 0), 0);
    return total > 0 ? ((row.segments?.[key] || 0) / total) * 100 : 0;
  }

  private buildCharts(areaSegments: any[], areaByRegion: any[]) {
    this.txByAreaOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      grid: { top: 20, right: 20, bottom: 40, left: 50 },
      xAxis: { 
        type: 'category', 
        data: areaSegments.map(a => a.range),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 20, fontSize: 11, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{
        type: 'bar', barWidth: 40,
        data: areaSegments.map((a, idx) => ({ value: a.transactions, itemStyle: { color: AREA_COLORS[idx], borderRadius: [4, 4, 0, 0] } })),
      }],
    });

    this.avgByAreaOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => { const p = Array.isArray(params) ? params[0] : params; return `${p.name}<br/>${formatCompact(p.value)} ر.س`; } },
      grid: { top: 20, right: 20, bottom: 40, left: 60 },
      xAxis: { 
        type: 'category', 
        data: areaSegments.map(a => a.range),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 20, fontSize: 11, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: (v: number) => formatCompact(v) },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: [{ type: 'bar', barWidth: 40, data: areaSegments.map(a => a.avgValue), itemStyle: { color: CHART_COLORS.positive, borderRadius: [4, 4, 0, 0] } }],
    });

    this.areaByRegionOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      legend: { data: AREA_LABELS, bottom: 0, textStyle: { fontSize: 10, color: CHART_COLORS.axis } },
      grid: { top: 10, right: 20, bottom: 50, left: 120 },
      xAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      yAxis: { 
        type: 'category', 
        data: areaByRegion.map((r: any) => r.region),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis }
      },
      series: AREA_KEYS.map((key, idx) => ({
        name: AREA_LABELS[idx], type: 'bar' as const, stack: 'total', barWidth: 20,
        data: areaByRegion.map((r: any) => r.segments?.[key] ?? 0),
        itemStyle: { color: AREA_COLORS[idx] },
      })),
    });
  }
}
