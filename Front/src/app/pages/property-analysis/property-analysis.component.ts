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
import { propertyTypeColor } from '../../utils/property-type-colors';
import type { EChartsOption } from 'echarts';

@Component({
  selector: 'app-property-analysis',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, LayoutComponent, ChartCardComponent, ChartEmptyStateComponent],
  templateUrl: './property-analysis.component.html',
})
export class PropertyAnalysisComponent implements OnInit, OnDestroy {
  propertyTypes: any[] = [];
  propertyTypeByNationality: any[] = [];
  propertyTypeByRegion: any[] = [];
  typeColumns: string[] = [];
  colorOf = propertyTypeColor;
  formatCount = formatCount;

  hasData = false;
  donutOptions: EChartsOption = {};
  valueBarOptions: EChartsOption = {};
  trendOptions: EChartsOption = {};

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
    this.api.getProperties(this.filterState.toQueryParams()).subscribe(data => {
      this.hasData = data.propertyTypes?.length > 0;
      this.propertyTypes = data.propertyTypes;
      this.propertyTypeByNationality = data.propertyTypeByNationality;
      this.propertyTypeByRegion = data.propertyTypeByRegion;
      this.typeColumns = (data.propertyTypes || []).map((pt: any) => pt.name);
      this.buildCharts(data.propertyTypes, data.propertyTypeByMonth);
    });
  }

  private buildCharts(propertyTypes: any[], propertyTypeByMonth: any[]) {
    this.donutOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'item', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      series: [{
        type: 'pie', radius: ['45%', '75%'], padAngle: 2,
        data: propertyTypes.map(pt => ({ value: pt.transactions, name: pt.name, itemStyle: { color: propertyTypeColor(pt.name) } })),
        label: { show: false },
      }],
    });

    this.valueBarOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, formatter: (params: any) => { const p = Array.isArray(params) ? params[0] : params; return `${p.name}<br/>${formatCompact(p.value)} ر.س`; } },
      grid: { top: 10, right: 36, bottom: 20, left: 120 },
      xAxis: { 
        type: 'value',
        splitNumber: 3,
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, formatter: (v: number) => formatCompact(v), hideOverlap: true },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      yAxis: { 
        type: 'category', 
        data: [...propertyTypes].sort((a, b) => a.value - b.value).map(p => p.name),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, width: 110, overflow: 'break' }
      },
      series: [{
        type: 'bar', barWidth: 22,
        data: [...propertyTypes].sort((a, b) => a.value - b.value).map(p => ({ value: p.value, itemStyle: { color: propertyTypeColor(p.name), borderRadius: [0, 4, 4, 0] } })),
      }],
    });

    this.trendOptions = applyRtl({
      ...baseChartTheme(),
      tooltip: { trigger: 'axis', backgroundColor: CHART_COLORS.tooltipBg, borderWidth: 0, textStyle: { color: '#fff' }, valueFormatter: (v: any) => formatCount(Number(v)) },
      legend: { data: this.typeColumns, bottom: 0, textStyle: { fontSize: 11, color: CHART_COLORS.axis } },
      grid: { top: 20, right: 20, bottom: 70, left: 40 },
      xAxis: { 
        type: 'category', 
        data: propertyTypeByMonth.map((m: any) => m.label),
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis, rotate: 30, fontSize: 10, hideOverlap: true }
      },
      yAxis: { 
        type: 'value',
        axisLine: { lineStyle: { color: CHART_COLORS.axis } },
        axisLabel: { color: CHART_COLORS.axis },
        splitLine: { lineStyle: { color: CHART_COLORS.grid } }
      },
      series: this.typeColumns.map(key => ({
        name: key, type: 'line' as const, smooth: true,
        data: propertyTypeByMonth.map((m: any) => m.types?.[key] ?? 0),
        itemStyle: { color: propertyTypeColor(key) }, lineStyle: { width: 2 },
      })),
    });
  }
}
