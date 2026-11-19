import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MonitorItem } from '@/models/rental-controls';

@Component({
  selector: 'app-what-to-monitor',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="rounded-[20px] bg-white p-5 h-full"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight mb-4">ما يجب مراقبته</h2>

      <div class="overflow-hidden rounded-xl" style="border: 1px solid #F1F5F9">
        <table class="w-full text-right">
          <thead>
            <tr style="background-color: #F8FAFC">
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">المؤشر</th>
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">الوضع الحالي</th>
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">الاتجاه</th>
              <th class="px-3 py-2 text-sm font-bold text-[#4B5563]">الأولوية</th>
            </tr>
          </thead>
          <tbody>
            @for (item of items; track $index) {
              <tr style="border-top: 1px solid #F1F5F9">
                <td class="px-3 py-2 text-sm font-bold text-[#0F172A]">{{ item.item }}</td>
                <td class="px-3 py-2 text-sm text-[#334155] whitespace-nowrap">{{ item.currentStatus }}</td>
                <td class="px-3 py-2">
                  <span class="w-3.5 h-3.5 flex items-center justify-center" [style.color]="trendIcon(item.trend).color">
                    <i [class]="trendIcon(item.trend).icon"></i>
                  </span>
                </td>
                <td class="px-3 py-2">
                  <span
                    class="inline-flex items-center gap-1 text-sm font-bold px-2 py-0.5 rounded-full whitespace-nowrap"
                    [style.backgroundColor]="priorityConfig[item.priority].bg"
                    [style.color]="priorityConfig[item.priority].color"
                  >
                    {{ item.priority }}
                  </span>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class WhatToMonitorComponent {
  @Input({ required: true }) items!: MonitorItem[];

  readonly priorityConfig: Record<string, { color: string; bg: string }> = {
    'عالي': { color: '#DC2626', bg: '#FEF2F2' },
    'متوسط': { color: '#D97706', bg: '#FFFBEB' },
    'منخفض': { color: '#059669', bg: '#ECFDF5' },
    '—': { color: '#4B5563', bg: '#F1F5F9' },
  };

  trendIcon(trend: 'up' | 'down' | 'stable'): { icon: string; color: string } {
    if (trend === 'up') return { icon: 'ri-arrow-up-line', color: '#DC2626' };
    if (trend === 'down') return { icon: 'ri-arrow-down-line', color: '#059669' };
    return { icon: 'ri-subtract-line', color: '#4B5563' };
  }
}
