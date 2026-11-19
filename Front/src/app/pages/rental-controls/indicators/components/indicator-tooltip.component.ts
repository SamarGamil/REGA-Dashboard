import { Component, ElementRef, HostListener, Input, signal } from '@angular/core';

@Component({
  selector: 'app-indicator-tooltip',
  standalone: true,
  template: `
    <div class="relative inline-flex z-30">
      <button
        (click)="toggle($event)"
        class="w-4 h-4 flex items-center justify-center rounded-full text-[#4B5563] hover:text-[#00848D] hover:bg-[#F0F4F8] transition-colors cursor-pointer"
        [attr.aria-label]="'معلومات عن ' + title"
      >
        <i class="ri-information-line text-sm"></i>
      </button>
      @if (open()) {
        <div
          class="absolute right-0 top-full mt-2 w-[280px] p-4 rounded-xl bg-white text-right"
          style="box-shadow: 0 4px 24px rgba(0,47,86,0.10), 0 1px 3px rgba(0,47,86,0.06); border: 1px solid #E5EBEF"
        >
          <p class="text-sm text-[#4B5563] leading-relaxed mb-3">{{ definition }}</p>
          <div class="h-px bg-[#E5EBEF] mb-3"></div>
          <p class="text-sm font-bold text-[#002F56] mb-2">الحدود المرجعية للمراقبة:</p>
          <div class="space-y-1.5">
            @for (line of lines(); track $index) {
              <p class="text-sm text-[#0F2137] leading-relaxed flex items-start gap-2">
                <span class="w-2 h-2 rounded-full mt-1.5 flex-shrink-0" [style.backgroundColor]="getDotColor(line)"></span>
                {{ line.trim() }}
              </p>
            }
          </div>
          @if (orderingLabel) {
            <div class="h-px bg-[#E5EBEF] my-3"></div>
            <p class="text-sm text-[#0F2137] leading-relaxed flex items-start gap-2">
              <i class="ri-sort-desc text-sm mt-0.5 flex-shrink-0" style="color: #00848D"></i>
              {{ orderingLabel }}
            </p>
          }
        </div>
      }
    </div>
  `,
})
export class IndicatorTooltipComponent {
  @Input() title = '';
  @Input() definition = '';
  @Input() thresholdRule = '';
  @Input() orderingLabel = '';

  readonly open = signal(false);

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  lines(): string[] {
    return this.thresholdRule.split(' — ');
  }

  toggle(e: Event): void {
    e.stopPropagation();
    this.open.set(!this.open());
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(e: MouseEvent): void {
    if (!this.open()) return;
    if (!this.host.nativeElement.contains(e.target as Node)) {
      this.open.set(false);
    }
  }

  getDotColor(line: string): string {
    if (line.includes('ضمن الحد')) return '#059669';
    if (line.includes('متابعة')) return '#D97706';
    if (line.includes('دون الحد') || line.includes('متجاوز') || line.includes('تجاوز')) return '#DC2626';
    return '#6B7280';
  }
}
