import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CityAnalysisData } from '@/models/rental-controls';

@Component({
  selector: 'app-executive-summary',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="rounded-[20px] bg-white p-5 h-full flex flex-col"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <!-- Title -->
      <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight pb-3 mb-4">
        الملخص التنفيذي
      </h2>

      <!-- Market Status Bar -->
      <div
        class="flex items-center gap-3 px-4 py-2.5 rounded-lg mb-4"
        [style.backgroundColor]="data.executiveSummary.statusBadge.bgColor"
        [style.borderRight]="'4px solid ' + data.executiveSummary.statusBadge.color"
      >
        <span class="text-sm font-bold text-[#334155]">حالة السوق:</span>
        <span
          class="text-sm font-extrabold"
          [style.color]="data.executiveSummary.statusBadge.color"
        >
          {{ data.executiveSummary.statusBadge.label }}
        </span>
      </div>

      <!-- Bullets -->
      <ul class="space-y-2.5 flex-1">
        @for (bullet of data.executiveSummary.bullets; track $index) {
          <li class="flex gap-2.5 text-sm text-[#334155] leading-[1.6]">
            <span
              class="mt-1.5 w-2 h-2 rounded-sm flex-shrink-0"
              style="background-color: #00848D"
            ></span>
            <span>{{ stripNumber(bullet) }}</span>
          </li>
        }
      </ul>
    </div>
  `,
})
export class ExecutiveSummaryComponent {
  @Input({ required: true }) data!: CityAnalysisData;

  stripNumber(bullet: string): string {
    // Newer workbooks prefix the number with a right-to-left mark ("\u200F1. ").
    return bullet.replace(/^[\s\u200E\u200F]*\d+[.\-)]\s*/, '');
  }
}
