import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CityAnalysisData } from '@/models/rental-controls';
import { SentimentAnalysisModalComponent } from './sentiment-analysis-modal.component';

@Component({
  selector: 'app-media-monitoring',
  standalone: true,
  imports: [CommonModule, SentimentAnalysisModalComponent],
  template: `
    @if (!data.mediaMonitoring.available) {
      <div
        class="rounded-[20px] bg-white p-5 h-full flex items-center justify-center"
        style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
      >
        <div class="text-center">
          <span class="w-8 h-8 flex items-center justify-center mx-auto mb-2">
            <i class="ri-newspaper-line text-xl text-[#4B5563]"></i>
          </span>
          <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight mb-1">الرصد الإعلامي</h2>
          <p class="text-sm text-[#4B5563]">لا يتوفر رصد إعلامي لهذه المدينة في هذه الفترة.</p>
        </div>
      </div>
    } @else {
      <div
        class="rounded-[20px] bg-white p-5 h-full flex flex-col"
        style="box-shadow: 0 1px 3px rgba(0,47,86,0.03), 0 8px 24px rgba(0,47,86,0.04); border: 1px solid #E7ECF2"
      >
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-base font-extrabold text-[#0F172A] tracking-tight">الرصد الإعلامي</h2>
          @if (data.mediaMonitoring.monthLabel) {
            <span class="text-xs font-semibold text-[#64748B] bg-[#F1F5F9] rounded-full px-2.5 py-1">{{ data.mediaMonitoring.monthLabel }}</span>
          }
        </div>

        <div class="flex items-center gap-2 mb-4">
          <div class="flex-1 rounded-lg p-2 text-center" style="background-color: #F0FDF4">
            <p class="text-lg font-extrabold" style="color: #059669">{{ data.mediaMonitoring.positivePct }}%</p>
            <p class="text-sm font-medium text-[#4B5563]">إيجابي</p>
          </div>
          <div class="flex-1 rounded-lg p-2 text-center" style="background-color: #F8FAFC">
            <p class="text-lg font-extrabold text-[#4B5563]">{{ data.mediaMonitoring.neutralPct }}%</p>
            <p class="text-sm font-medium text-[#4B5563]">محايد</p>
          </div>
          <div class="flex-1 rounded-lg p-2 text-center" style="background-color: #FFF5F5">
            <p class="text-lg font-extrabold" style="color: #DC2626">{{ data.mediaMonitoring.negativePct }}%</p>
            <p class="text-sm font-medium text-[#4B5563]">سلبي</p>
          </div>
        </div>

        <div class="w-full h-1.5 rounded-full flex overflow-hidden mb-4" style="background-color: #F1F5F9">
          <div class="h-full rounded-l-full" [style.width.%]="data.mediaMonitoring.positivePct" style="background-color: #059669"></div>
          <div class="h-full" [style.width.%]="data.mediaMonitoring.neutralPct" style="background-color: #CBD5E1"></div>
          <div class="h-full rounded-r-full" [style.width.%]="data.mediaMonitoring.negativePct" style="background-color: #DC2626"></div>
        </div>

        <ul class="space-y-2 mb-4">
          @for (headline of data.mediaMonitoring.topHeadlines.slice(0, 5); track $index; let idx = $index) {
            <li class="flex items-start gap-2 text-sm text-[#475569] leading-[1.5]">
              <span class="mt-0.5 text-sm font-bold text-[#4B5563] flex-shrink-0">{{ idx + 1 }}.</span>
              <span>{{ headline }}</span>
            </li>
          }
        </ul>

        <div class="border-t border-[#EEF1F5] mt-auto pt-4">
          <button
            (click)="modalOpen.set(true)"
            class="w-full flex items-center justify-between px-3.5 py-2.5 rounded-xl cursor-pointer whitespace-nowrap transition-colors duration-150 bg-[#F8FAFC] hover:bg-[#F0F4F8] text-[#0F766E] group"
            aria-label="فتح تحليل الانطباعات التفصيلي"
          >
            <span class="flex items-center gap-2.5">
              <span class="w-5 h-5 flex items-center justify-center text-[#0F766E]">
                <i class="ri-pie-chart-2-line text-base"></i>
              </span>
              <span class="text-sm font-bold">تحليل الانطباعات</span>
            </span>
            <span class="w-5 h-5 flex items-center justify-center text-[#0F766E] transition-transform duration-200 group-hover:translate-x-[-2px]">
              <i class="ri-arrow-left-line text-base"></i>
            </span>
          </button>
        </div>
      </div>

      @if (modalOpen()) {
        <app-sentiment-analysis-modal [cityName]="data.cityName" [data]="data.mediaMonitoring" (close)="modalOpen.set(false)" />
      }
    }
  `,
})
export class MediaMonitoringComponent {
  @Input({ required: true }) data!: CityAnalysisData;

  modalOpen = signal(false);
}
