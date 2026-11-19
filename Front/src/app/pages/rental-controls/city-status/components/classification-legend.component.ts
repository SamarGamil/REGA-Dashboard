import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { ClassificationLegendItem } from '@/models/rental-controls';

@Component({
  selector: 'app-classification-legend',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="bg-white rounded-lg border border-background-200 p-4" dir="rtl">
      <div class="flex items-center justify-between mb-3">
        <h3 class="text-sm font-bold text-foreground-900">دليل تصنيف النمو الإيجاري</h3>
        <a
          routerLink="/rental-indicators/methodology"
          class="text-xs text-primary-600 hover:text-primary-700 flex items-center gap-1 whitespace-nowrap cursor-pointer"
        >
          <span>قراءة المزيد عن المنهجية</span>
          <span class="w-3.5 h-3.5 flex items-center justify-center">
            <i class="ri-arrow-left-line text-xs"></i>
          </span>
        </a>
      </div>
      <div class="flex flex-wrap items-center gap-4">
        @for (item of legend; track item.key) {
          <div class="flex items-center gap-2">
            <div [class]="'w-5 h-5 rounded ' + item.colorClass + ' border border-background-200'"></div>
            <div class="flex items-center gap-2">
              <span class="text-sm font-medium text-foreground-800">{{ item.label }}</span>
              <span class="text-xs text-foreground-400">{{ item.range }}</span>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class ClassificationLegendComponent {
  @Input() legend: ClassificationLegendItem[] = [];
}
