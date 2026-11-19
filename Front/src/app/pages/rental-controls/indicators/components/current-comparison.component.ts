import { Component, EventEmitter, Input, Output } from '@angular/core';
import { IndicatorPanelData } from '@/models/rental-controls';
import { IndicatorPanelComponent } from './indicator-panel.component';

@Component({
  selector: 'app-current-comparison',
  standalone: true,
  imports: [IndicatorPanelComponent],
  template: `
    <section class="mb-6">
      <h2 class="text-[16px] font-extrabold text-[#002F56] mb-3 flex items-center gap-2">
        <span class="w-1 h-5 rounded-full" style="background-color: #00848D"></span>
        ترتيب المدن لمؤشرات الضوابط الإيجارية
      </h2>

      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6 gap-3">
        @for (ind of indicators; track ind.id) {
          <app-indicator-panel
            [data]="ind"
            (indicatorClick)="onIndicatorClick.emit($event)"
            [isActive]="activeIndicatorId === ind.id"
          />
        }
      </div>
    </section>
  `,
})
export class CurrentComparisonComponent {
  @Input({ required: true }) indicators: IndicatorPanelData[] = [];
  @Input() activeIndicatorId: string | null = null;
  @Output() onIndicatorClick = new EventEmitter<string>();
}
