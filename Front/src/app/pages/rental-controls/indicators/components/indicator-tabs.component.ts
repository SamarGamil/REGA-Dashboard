import { Component, EventEmitter, Input, Output } from '@angular/core';

interface Tab {
  id: string;
  title: string;
}

@Component({
  selector: 'app-indicator-tabs',
  standalone: true,
  host: { class: 'block min-w-0' },
  template: `
    <div
      class="flex flex-wrap md:flex-nowrap items-center gap-1 p-1.5 rounded-[26px] md:overflow-x-auto"
      style="background-color: #EEF2F5; min-height: 52px"
    >
      @for (tab of tabs; track tab.id) {
        @let isActive = activeTab === tab.id;
        <button
          (click)="onTabChange.emit(tab.id)"
          [class]="'px-3.5 md:px-5 py-2 md:py-2.5 rounded-full text-[13px] md:text-[15px] transition-all duration-200 whitespace-nowrap cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-[#00848D] focus-visible:ring-offset-1 ' + (isActive ? 'font-bold text-white' : 'font-semibold text-[#465466] hover:bg-[rgba(0,132,141,0.07)] hover:text-[#00848D]')"
          [style.backgroundColor]="isActive ? '#00848D' : ''"
        >
          {{ tab.title }}
        </button>
      }
    </div>
  `,
})
export class IndicatorTabsComponent {
  @Input({ required: true }) tabs: Tab[] = [];
  @Input() activeTab = '';
  @Output() onTabChange = new EventEmitter<string>();
}
