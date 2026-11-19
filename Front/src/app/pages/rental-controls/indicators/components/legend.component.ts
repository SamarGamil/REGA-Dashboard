import { Component, Input } from '@angular/core';

interface Threshold {
  label: string;
  cssClass: string;
}

@Component({
  selector: 'app-legend',
  standalone: true,
  template: `
    <div class="flex items-center gap-4 text-sm mt-3">
      <span class="font-medium text-[#4B5563]">الدلالة:</span>
      @for (t of thresholds; track t.label) {
        <div class="flex items-center gap-1.5">
          <span [class]="'w-3 h-3 rounded-sm ' + firstClass(t.cssClass)"></span>
          <span class="text-[#0F2137]">{{ t.label }}</span>
        </div>
      }
    </div>
  `,
})
export class LegendComponent {
  @Input({ required: true }) thresholds: Threshold[] = [];

  firstClass(cssClass: string): string {
    return cssClass.split(' ')[0];
  }
}
