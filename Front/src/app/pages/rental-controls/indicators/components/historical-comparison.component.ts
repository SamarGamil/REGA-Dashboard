import { Component, ElementRef, Input, ViewChild, computed, signal } from '@angular/core';
import { HistoricalIndicatorData } from '@/models/rental-controls';
import { IndicatorTabsComponent } from './indicator-tabs.component';
import { HistoricalHeatmapComponent } from './historical-heatmap.component';
import { ExecutiveInsightComponent } from './executive-insight.component';
import { LegendComponent } from './legend.component';
import { MicroSummaryComponent } from './micro-summary.component';

@Component({
  selector: 'app-historical-comparison',
  standalone: true,
  imports: [
    IndicatorTabsComponent,
    HistoricalHeatmapComponent,
    ExecutiveInsightComponent,
    LegendComponent,
    MicroSummaryComponent,
  ],
  template: `
    <section #sectionRef class="mb-6">
      <div class="border-t border-[#E5EBEF] pt-6 mb-4">
        <h2 class="text-[16px] font-extrabold text-[#002F56] mb-1 flex items-center gap-2">
          <span class="w-1 h-5 rounded-full" style="background-color: #005257"></span>
          مقارنة مؤشرات الضوابط الإيجارية
        </h2>
        <p class="text-sm text-[#4B5563] mb-4">
          تطور مؤشرات الضوابط الإيجارية على مستوى المدن خلال السنوات المتاحة.
        </p>
      </div>

      <div class="mb-3">
        <app-indicator-tabs
          [tabs]="tabs()"
          [activeTab]="activeTab()"
          (onTabChange)="onTabChange($event)"
        />
      </div>

      <app-micro-summary [data]="activeData()" />

      <div
        class="rounded-xl bg-white p-4"
        style="border: 1px solid #E5EBEF; box-shadow: 0 1px 3px rgba(0,47,86,0.03)"
      >
        <app-historical-heatmap [data]="activeData()" />
        <app-legend [thresholds]="activeData().thresholds" />
      </div>

      <app-executive-insight [data]="activeData()" />
    </section>
  `,
})
export class HistoricalComparisonComponent {
  @Input({ required: true })
  set historicalData(value: HistoricalIndicatorData[]) {
    this._historicalData.set(value);
    if (!this.activeTab() && value.length) {
      this.activeTab.set(value[0].id);
    }
  }
  get historicalData(): HistoricalIndicatorData[] {
    return this._historicalData();
  }
  private readonly _historicalData = signal<HistoricalIndicatorData[]>([]);

  @Input()
  set initialIndicatorId(value: string | undefined) {
    if (value && value !== this.activeTab()) {
      this.activeTab.set(value);
      setTimeout(() => {
        this.sectionRef?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }, 100);
    }
  }

  @ViewChild('sectionRef') sectionRef?: ElementRef<HTMLElement>;

  readonly activeTab = signal('');

  readonly activeData = computed<HistoricalIndicatorData>(() => {
    const data = this._historicalData();
    return data.find((d) => d.id === this.activeTab()) || data[0];
  });

  readonly tabs = computed(() => this._historicalData().map((d) => ({ id: d.id, title: d.title })));

  onTabChange(id: string): void {
    this.activeTab.set(id);
  }
}
