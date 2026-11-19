import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CityJourneyStage } from '@/models/rental-controls';

interface JourneyStyle {
  dotBg: string;
  dotBorder: string;
  textColor: string;
  fontWeight: string;
}

@Component({
  selector: 'app-city-journey',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="rounded-[20px] bg-white p-5 mb-5"
      style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
    >
      <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight mb-5">رحلة المدينة</h2>

      <div class="relative px-4">
        <div class="flex items-start justify-between relative">
          <div class="absolute top-3.5 left-[8%] right-[8%] h-0.5" style="background-color: #E7ECF2"></div>

          @for (stage of stages; track $index) {
            @let s = styleFor(stage);
            <div class="flex flex-col items-center gap-1.5 flex-1 relative z-10">
              <div
                class="w-7 h-7 rounded-full flex items-center justify-center transition-all"
                [style.backgroundColor]="s.dotBg"
                [style.border]="'2px solid ' + s.dotBorder"
                [style.boxShadow]="stage.isCurrent ? '0 0 0 4px rgba(0,132,141,0.12)' : 'none'"
              >
                @if (stage.isCompleted) {
                  <i class="ri-check-line text-white text-xs"></i>
                } @else if (stage.isCurrent) {
                  <div class="w-2 h-2 rounded-full bg-white"></div>
                } @else {
                  <div class="w-1.5 h-1.5 rounded-full" style="background-color: #CBD5E1"></div>
                }
              </div>
              <span
                [class]="'text-sm ' + s.fontWeight + ' text-center leading-[1.3] max-w-[70px] min-h-[55px] flex items-center justify-center'"
                [style.color]="s.textColor"
              >
                {{ stage.label }}
              </span>
              <span
                class="block h-3.5 text-[11px] leading-[1.2] text-center whitespace-nowrap max-w-[90px]"
                [style.color]="stage.isCurrent ? '#0F766E' : '#94A3B8'"
                [style.fontWeight]="stage.isCurrent ? 600 : 400"
                [style.visibility]="stage.date ? 'visible' : 'hidden'"
              >
                {{ stage.date || '' }}
              </span>
            </div>
          }
        </div>
      </div>
    </div>
  `,
})
export class CityJourneyComponent {
  @Input({ required: true }) stages!: CityJourneyStage[];

  styleFor(stage: CityJourneyStage): JourneyStyle {
    if (stage.isCurrent) {
      return { dotBg: '#00848D', dotBorder: '#00848D', textColor: '#00848D', fontWeight: 'font-extrabold' };
    }
    if (stage.isCompleted) {
      return { dotBg: '#00A488', dotBorder: '#00A488', textColor: '#0F172A', fontWeight: 'font-bold' };
    }
    return { dotBg: '#E7ECF2', dotBorder: '#CBD5E1', textColor: '#64748B', fontWeight: 'font-medium' };
  }
}
