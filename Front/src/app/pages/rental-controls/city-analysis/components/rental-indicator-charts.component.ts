import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RentalIndicatorChart } from '@/models/rental-controls';
import { ChartDirectionArrowComponent } from '@/components/base/chart-direction-arrow/chart-direction-arrow.component';

@Component({
  selector: 'app-rental-indicator-charts',
  standalone: true,
  imports: [CommonModule, ChartDirectionArrowComponent],
  template: `
    <div
      class="rounded-[20px] bg-white p-5 h-full flex flex-col"
      dir="rtl"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight mb-4">الرسوم البيانية للمؤشرات الإيجارية</h2>
      <div class="flex flex-col gap-4 flex-1 justify-center">
        @for (chart of charts; track chart.label) {
          <div class="rounded-xl p-2 flex flex-col" style="background-color: #F8FAFC; border: 1px solid #F1F5F9">
            <div class="flex items-center justify-between mb-1.5">
              <span class="text-sm font-bold text-[#0F172A] whitespace-nowrap leading-tight">{{ chart.label }}</span>
              <span
                class="text-sm font-extrabold whitespace-nowrap"
                [style.color]="chart.status === 'سلبي' ? '#DC2626' : '#0A2540'"
              >
                {{ chart.value }}
              </span>
            </div>

            <div class="flex items-end justify-center gap-[5px]" style="height: 118px">
              @for (bar of barsFor(chart); track bar.period) {
                <div class="flex-1 max-w-[54px] flex flex-col items-center justify-end" style="height: 100%">
                  <span class="text-[12px] font-extrabold text-[#0F172A] leading-none mb-1 tabular-nums">
                    {{ bar.value }}
                  </span>
                  <div class="flex flex-col items-center flex-1 justify-end w-full">
                    <div
                      class="w-full rounded-[3px]"
                      [style.height.px]="bar.barH"
                      [style.background]="bar.isLast
                        ? (chart.status === 'سلبي' ? '#DC2626' : '#4F86BE')
                        : 'linear-gradient(180deg, #BBD8F0 0%, #7FA9D4 100%)'"
                      style="min-height: 3px"
                    ></div>
                  </div>
                  <span class="text-[10px] text-[#4B5563] mt-1 leading-tight text-center">
                    <bdi>{{ bar.period }}</bdi>
                  </span>
                </div>
              }
            </div>
            <app-chart-direction-arrow class="mt-0.5" />
          </div>
        }
      </div>
    </div>
  `,
})
export class RentalIndicatorChartsComponent {
  @Input({ required: true }) charts: RentalIndicatorChart[] = [];

  barsFor(chart: RentalIndicatorChart) {
    const values = chart.points.map((p) => p.value);
    const maxVal = Math.max(...values, 0);
    const minVal = Math.min(...values, 0);
    const range = maxVal - minVal * 0.7 || 1;
    return chart.points.map((p) => ({
      period: p.period,
      value: p.value,
      isLast: p.isLast,
      barH: Math.max(5, ((p.value - minVal * 0.7) / range) * 88),
    }));
  }
}
