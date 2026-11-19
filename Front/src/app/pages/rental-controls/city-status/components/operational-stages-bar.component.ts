import { Component, EventEmitter, Input, Output } from '@angular/core';
import type { OperationalStage } from '@/models/rental-controls';

@Component({
  selector: 'app-operational-stages-bar',
  standalone: true,
  template: `
    <div class="bg-white rounded-lg border border-background-200 p-1" dir="rtl">
      <div class="flex flex-wrap items-center gap-0.5">
        @for (stage of stages; track stage.key) {
          <button
            (click)="onStageChange.emit(stage.key === activeStage ? '' : stage.key)"
            [class]="'flex items-center gap-2 px-3 py-1.5 rounded-full text-sm font-medium transition-colors duration-150 whitespace-nowrap cursor-pointer ' + (activeStage === stage.key ? 'bg-primary-500 text-white' : 'text-foreground-600 hover:bg-background-100')"
          >
            <span>{{ stage.label }}</span>
            <span [class]="'text-xs w-5 h-5 flex items-center justify-center rounded-full ' + (activeStage === stage.key ? 'bg-white/20' : 'bg-background-100')">
              {{ stage.count }}
            </span>
          </button>
        }
      </div>
    </div>
  `,
})
export class OperationalStagesBarComponent {
  @Input() activeStage = '';
  @Input() stages: OperationalStage[] = [];
  @Output() onStageChange = new EventEmitter<string>();
}
