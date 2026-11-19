import { Component, Input, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';
import { MarketAnalysisTab } from '@/models/rental-controls';
import { applyRtl } from '@/utils/chart-theme';
import { ChartDirectionArrowComponent } from '@/components/base/chart-direction-arrow/chart-direction-arrow.component';

@Component({
  selector: 'app-market-analysis-tabs',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, ChartDirectionArrowComponent],
  template: `
    <div
      class="rounded-[20px] bg-white p-5 mb-5"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight mb-4">التحليل التفصيلي</h2>

      <div
        class="flex flex-wrap md:flex-nowrap items-center gap-1 p-1.5 rounded-[26px] mb-4 md:overflow-x-auto"
        style="background-color: #EEF2F5; min-height: 52px"
      >
        @for (tab of tabs; track tab.id) {
          <button
            (click)="activeTabId.set(tab.id)"
            [class]="'px-3.5 md:px-5 py-2 md:py-2.5 rounded-full text-[13px] md:text-[15px] transition-all duration-200 whitespace-nowrap cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-[#00848D] focus-visible:ring-offset-1 ' + (activeTabId() === tab.id ? 'font-bold text-white' : 'font-semibold text-[#465466] hover:bg-[rgba(0,132,141,0.07)] hover:text-[#00848D]')"
            [style.backgroundColor]="activeTabId() === tab.id ? '#00848D' : null"
          >
            {{ tab.label }}
          </button>
        }
      </div>

      <p class="text-sm text-[#475569] leading-[1.6] mb-4 pr-1" style="border-right: 3px solid #00848D; padding-right: 10px">
        {{ active().insight }}
      </p>

      <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
        <div>
          <div class="h-[220px]">
            <div echarts [options]="chartOption()" class="h-full w-full" style="height: 100%"></div>
          </div>
          <app-chart-direction-arrow />
        </div>

        <div class="overflow-auto rounded-xl max-h-[220px]" style="border: 1px solid #F1F5F9">
          <table class="w-full text-right">
            <thead>
              <tr style="background-color: #F8FAFC">
                @for (col of active().tableColumns; track $index) {
                  <th class="px-3 py-2.5 text-sm font-bold text-[#4B5563] whitespace-nowrap">{{ col }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of active().tableRows; track $index) {
                <tr style="border-top: 1px solid #F1F5F9">
                  @for (cell of row; track $index) {
                    <td class="px-3 py-2.5 text-sm whitespace-nowrap"
                        [ngClass]="$first ? 'font-bold text-[#334155]' : 'text-[#0F172A]'"><bdi>{{ cell }}</bdi></td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
})
export class MarketAnalysisTabsComponent {
  @Input({ required: true }) set tabs(value: MarketAnalysisTab[]) {
    this._tabs.set(value);
    if (!value.some((t) => t.id === this.activeTabId()) && value.length) {
      this.activeTabId.set(value[0].id);
    }
  }
  get tabs(): MarketAnalysisTab[] {
    return this._tabs();
  }
  private _tabs = signal<MarketAnalysisTab[]>([]);

  activeTabId = signal<string>('');

  active = computed<MarketAnalysisTab>(() => {
    return this._tabs().find((t) => t.id === this.activeTabId()) || this._tabs()[0];
  });

  private static readonly PALETTE = ['#00848D', '#75CEC8', '#F4A261', '#E76F51', '#8AB4F8', '#E9C46A', '#264653'];

  chartOption = computed<EChartsOption>(() => {
    const a = this.active();
    if (!a) return {};
    const palette = MarketAnalysisTabsComponent.PALETTE;
    const isBar = a.chartType === 'bar';
    const multi = a.series.length > 1;

    const series = a.series.map((s, i) => {
      const color = palette[i % palette.length];
      const base: Record<string, unknown> = { name: s.name, data: s.data };
      if (isBar) {
        return {
          ...base,
          type: 'bar',
          stack: a.stacked ? 'total' : undefined,
          barMaxWidth: 26,
          itemStyle: { color, borderRadius: a.stacked ? 0 : [4, 4, 0, 0] },
        };
      }
      return {
        ...base,
        type: 'line',
        smooth: true,
        symbol: 'circle',
        symbolSize: 7,
        itemStyle: { color: '#fff', borderColor: color, borderWidth: 2 },
        lineStyle: { color, width: 2.5 },
        areaStyle: multi ? undefined : { color: 'rgba(0,132,141,0.08)' },
      };
    });

    if (a.threshold && series.length) {
      (series[0] as Record<string, unknown>)['markLine'] = {
        silent: true,
        symbol: 'none',
        lineStyle: { type: 'dashed', color: '#8B1E3F', width: 1.5 },
        label: { formatter: a.threshold.label, position: 'insideEndTop', color: '#8B1E3F', fontSize: 12 },
        data: [{ yAxis: a.threshold.value }],
      };
    }

    return applyRtl({
      color: palette,
      grid: { left: 8, right: 16, top: 28, bottom: multi ? 52 : 8, containLabel: true },
      tooltip: {
        trigger: 'axis',
        backgroundColor: '#0F172A',
        borderColor: '#0F172A',
        textStyle: { color: '#fff' },
        extraCssText: 'box-shadow: 0 4px 12px rgba(0,0,0,0.15); border-radius: 8px;',
      },
      legend: multi
        ? { show: true, bottom: 0, itemGap: 10, itemWidth: 12, itemHeight: 8, textStyle: { color: '#64748B', fontSize: 11 }, icon: 'roundRect' }
        : { show: false },
      xAxis: {
        type: 'category',
        boundaryGap: isBar,
        data: a.categories,
        axisLine: { lineStyle: { color: '#E7ECF2' } },
        axisTick: { show: false },
        axisLabel: { fontSize: 11, color: '#64748B', hideOverlap: true, interval: 'auto' },
      },
      yAxis: {
        type: 'value',
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { fontSize: 12, color: '#64748B' },
        splitLine: { lineStyle: { type: 'dashed', color: '#F1F5F9' } },
        ...(a.threshold
          ? { max: (v: { max: number }) => Math.ceil(Math.max(v.max, a.threshold!.value) * 1.1) }
          : {}),
      },
      series: series as EChartsOption['series'],
    });
  });
}
