import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { KpiItem } from '@/models/rental-controls';
import { frequencyBadge } from '@/utils/indicator-labels';

@Component({
  selector: 'app-kpi-cards',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="mb-5">
      <h3 class="text-[15px] font-bold text-[#0F172A] mb-4">مؤشرات الضوابط الإيجارية</h3>
      <div class="grid grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
        @for (kpi of sortedKpis(); track $index; let idx = $index) {
          @let status = statusConfig(kpi.status);
          @let trend = trendIcon(kpi.trend);
          <div
            class="rounded-2xl p-5 cursor-default relative overflow-hidden transition-all duration-300 hover:-translate-y-0.5 flex flex-col"
            style="background: linear-gradient(155deg, #002F56 0%, #003A5C 40%, #005257 100%)"
            [style.boxShadow]="hoveredKpi() === idx
              ? '0 8px 32px rgba(0,0,0,0.18), 0 2px 8px rgba(0,0,0,0.12)'
              : '0 2px 12px rgba(0,0,0,0.10), 0 1px 3px rgba(0,0,0,0.08)'"
            (mouseenter)="hoveredKpi.set(idx)"
            (mouseleave)="hoveredKpi.set(null)"
          >
            <div class="absolute inset-0 overflow-hidden rounded-2xl pointer-events-none">
              <div
                class="absolute -top-6 -right-6 w-28 h-28 rounded-full opacity-[0.06]"
                style="background: radial-gradient(circle, rgba(255,255,255,1) 0%, transparent 70%)"
              ></div>
            </div>

            <div class="relative z-10 flex flex-col h-full">
              <p class="text-sm font-medium text-white/65 mb-3 leading-[1.4]">{{ kpi.label }}</p>

              <div class="flex items-end gap-1.5 mb-3">
                <div class="flex items-center gap-2">
                  <span
                    dir="ltr"
                    class="text-[26px] font-extrabold tracking-tight leading-none"
                    [style.color]="status.color"
                  >
                    {{ kpi.value }}
                  </span>
                </div>
                <span
                  class="text-sm font-bold mb-0.5 flex items-center gap-0.5"
                  [style.color]="trend.color"
                >
                  @if (kpi.trend !== 'stable') {
                    <span class="w-3 h-3 flex items-center justify-center">
                      <i [class]="trend.icon"></i>
                    </span>
                  }
                </span>
              </div>

              @let badge = frequencyBadge(kpi.frequency);
              @if (badge || kpi.periodLabel) {
                <div class="flex flex-wrap items-center gap-x-1.5 gap-y-1 mb-3">
                  @if (badge) {
                    <span
                      class="text-sm font-medium px-2 py-0.5 rounded-full whitespace-nowrap"
                      style="background-color: rgba(255,255,255,0.12); color: rgba(255,255,255,0.75)"
                    >{{ badge.label }}</span>
                  }
                  @if (kpi.periodLabel) {
                    <span class="text-xs text-white/55 whitespace-nowrap">{{ kpi.periodLabel }}</span>
                  }
                </div>
              }

              <div class="mt-auto flex items-center gap-1.5">
                <span
                  class="text-sm font-medium px-2 py-0.5 rounded-full whitespace-nowrap"
                  [style.backgroundColor]="status.pillBg"
                  [style.color]="status.pillColor"
                >
                  {{ kpi.status }}
                </span>
                <span class="text-sm text-white/45">{{ kpi.quarterlyChange }}</span>
              </div>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class KpiCardsComponent {
  @Input({ required: true }) set kpis(value: KpiItem[]) {
    this._kpis = value;
  }
  private _kpis: KpiItem[] = [];

  hoveredKpi = signal<number | null>(null);

  readonly frequencyBadge = frequencyBadge;

  private readonly orderMap: Record<string, number> = {
    'الزيادة الإيجارية': 0,
    'الشواغر الإيجارية': 1,
    'العائد الإيجاري': 2,
    'نسبة الإيجار إلى الدخل': 3,
    'العبء السكني لذوي الدخل المحدود': 4,
  };

  sortedKpis(): KpiItem[] {
    return [...this._kpis].sort((a, b) => {
      const orderA = this.orderMap[a.label] ?? 99;
      const orderB = this.orderMap[b.label] ?? 99;
      return orderA - orderB;
    });
  }

  statusConfig(status: string): { color: string; pillBg: string; pillColor: string } {
    const config: Record<string, { color: string; pillBg: string; pillColor: string }> = {
      'إيجابي': { color: '#6EE7B7', pillBg: 'rgba(110,231,183,0.15)', pillColor: '#6EE7B7' },
      'محايد': { color: 'rgba(255,255,255,0.75)', pillBg: 'rgba(255,255,255,0.12)', pillColor: 'rgba(255,255,255,0.75)' },
      'سلبي': { color: '#FCA5A5', pillBg: 'rgba(252,165,165,0.15)', pillColor: '#FCA5A5' },
    };
    return config[status] || config['محايد'];
  }

  trendIcon(trend: 'up' | 'down' | 'stable'): { icon: string; color: string } {
    if (trend === 'up') return { icon: 'ri-arrow-up-line', color: '#FCA5A5' };
    if (trend === 'down') return { icon: 'ri-arrow-down-line', color: '#93C5FD' };
    return { icon: 'ri-subtract-line', color: 'rgba(255,255,255,0.5)' };
  }
}
