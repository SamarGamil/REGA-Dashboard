import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExecutiveRecommendation } from '@/models/rental-controls';
import { NumberBadgeComponent } from '@/components/base/number-badge.component';

@Component({
  selector: 'app-executive-recommendations',
  standalone: true,
  imports: [CommonModule, NumberBadgeComponent],
  template: `
    <div
      class="rounded-[20px] bg-white p-5 h-full"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight mb-4">التوصيات التنفيذية</h2>

      <div class="space-y-3">
        @for (rec of recommendations; track rec.priority) {
          <div
            class="rounded-xl p-3.5 transition-all duration-300 hover:-translate-y-0.5 cursor-default"
            style="border: 1px solid #F1F5F9; background-color: #FAFBFC"
          >
            <div class="flex items-start gap-3">
              <app-number-badge [number]="rec.priority" size="compact" />
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2 mb-0.5">
                  <h3 class="text-sm font-extrabold text-[#0F172A]">{{ rec.recommendation }}</h3>
                  <span
                    class="text-sm font-bold px-1.5 py-0.5 rounded-full whitespace-nowrap"
                    [style.backgroundColor]="priorityBg(rec.priorityLabel)"
                    [style.color]="priorityColor(rec.priorityLabel)"
                  >
                    {{ rec.priorityLabel }}
                  </span>
                </div>
                <p class="text-sm text-[#4B5563] leading-[1.5]">{{ rec.description }}</p>
              </div>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class ExecutiveRecommendationsComponent {
  @Input({ required: true }) recommendations!: ExecutiveRecommendation[];

  priorityColor(label: string): string {
    if (label === 'عالية') return '#DC2626';
    if (label === 'متوسطة') return '#D97706';
    return '#059669';
  }

  priorityBg(label: string): string {
    if (label === 'عالية') return '#FEF2F2';
    if (label === 'متوسطة') return '#FFFBEB';
    return '#ECFDF5';
  }
}
