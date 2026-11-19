import { Component, Input } from '@angular/core';
import type { Recommendation } from '@/models/rental-controls';
import { NumberBadgeComponent } from '@/components/base/number-badge.component';

@Component({
  selector: 'app-recommendation-panel',
  standalone: true,
  imports: [NumberBadgeComponent],
  template: `
    <div class="bg-white rounded-[20px] border border-[#E7ECF2] p-6"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.04), 0 8px 24px rgba(0,47,86,0.04)"
      dir="rtl"
    >
      <div class="flex items-center gap-3 mb-5">
        <span class="w-8 h-8 rounded-xl flex items-center justify-center"
          style="background: linear-gradient(135deg, #00848D10 0%, #00A48810 100%)"
        >
          <i class="ri-lightbulb-line text-[#00848D] text-sm"></i>
        </span>
        <h3 class="text-sm font-extrabold text-[#0F172A]">التوصيات التنفيذية</h3>
      </div>

      <div class="space-y-5">
        @for (rec of recommendations; track $index; let index = $index) {
          <div class="flex items-start gap-3">
            <div class="mt-0.5">
              <app-number-badge [number]="index + 1" />
            </div>
            <div>
              <span class="inline-block text-sm font-bold text-[#005257] px-2.5 py-[3px] rounded-full mb-2"
                style="background: #E8F6F3"
              >
                {{ rec.city }}
              </span>
              <p class="text-sm text-[#4B5563] leading-[1.7]">{{ rec.text }}</p>
            </div>
          </div>
        }
      </div>

      <div class="mt-6 pt-5"
        style="border-top: 1px solid #E7ECF2"
      >
        <div class="flex items-start gap-2.5">
          <span class="w-4 h-4 flex items-center justify-center mt-0.5 text-[#4B5563]">
            <i class="ri-information-line text-sm"></i>
          </span>
          <p class="text-sm text-[#4B5563] leading-relaxed">
            تستند التوصيات إلى أحدث البيانات المتاحة لفترة {{ periodLabel }}.
          </p>
        </div>
      </div>
    </div>
  `,
})
export class RecommendationPanelComponent {
  @Input() recommendations: Recommendation[] = [];
  /** The period the page shows (the portal period selection). */
  @Input() periodLabel = '';
}
