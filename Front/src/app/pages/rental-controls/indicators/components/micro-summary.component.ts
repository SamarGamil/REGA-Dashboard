import { Component, Input, OnChanges } from '@angular/core';
import { HistoricalIndicatorData } from '@/models/rental-controls';
import { extremeCities, joinCityNames, largestChangeCities } from './historical-summary.utils';

interface SummaryVM {
  maxCityName: string;
  maxCityValue: string;
  maxChangeCityName: string;
  exceedCount: number;
  exceedLabel: string;
}

@Component({
  selector: 'app-micro-summary',
  standalone: true,
  template: `
    <div class="flex items-center gap-4 mb-4 text-sm">
      <div class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white border border-[#E5EBEF]">
        <span class="text-[#4B5563]">أعلى مدينة:</span>
        <span class="font-bold text-[#0F2137]">{{ summary.maxCityName }}</span>
        <span class="text-[#00848D] font-extrabold">{{ summary.maxCityValue }}</span>
      </div>
      <div class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white border border-[#E5EBEF]">
        <span class="text-[#4B5563]">أكبر تغير:</span>
        <span class="font-bold text-[#0F2137]">{{ summary.maxChangeCityName }}</span>
      </div>
      @if (summary.exceedCount > 0) {
        <div class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white border border-[#E05555]/20">
          <span class="text-[#4B5563]">{{ summary.exceedLabel }}:</span>
          <span class="font-bold text-[#E05555]">{{ summary.exceedCount }}</span>
        </div>
      }
    </div>
  `,
})
export class MicroSummaryComponent implements OnChanges {
  @Input({ required: true }) data!: HistoricalIndicatorData;

  summary: SummaryVM = { maxCityName: '', maxCityValue: '', maxChangeCityName: '', exceedCount: 0, exceedLabel: '' };

  ngOnChanges(): void {
    this.summary = this.computeSummary(this.data);
  }

  private computeSummary(data: HistoricalIndicatorData): SummaryVM {
    const { cities, years } = data;
    const lastYear = years[years.length - 1];

    let exceedCount = 0;

    for (const c of cities) {
      // Cities with no value for the latest year are excluded from the count.
      const cv = c.values[lastYear];
      if (cv == null) continue;
      for (const t of data.thresholds) {
        if ((t.label === 'دون الحد' || t.label === 'متجاوز' || t.label === 'تجاوز مرتفع') && cv >= t.min && cv < t.max) {
          exceedCount++;
          break;
        }
      }
    }

    // Lower-is-worse indicators (vacancy, yield) flag cities below the reference, not above it.
    const belowIsBreach = data.thresholds.some((t) => t.label === 'دون الحد');
    const top = extremeCities(cities, lastYear, 'max');
    return {
      maxCityName: joinCityNames(top.map((c) => c.cityName)) || '—',
      maxCityValue: top[0]?.displayValues[lastYear] ?? '',
      maxChangeCityName: joinCityNames(largestChangeCities(cities, years).map((c) => c.cityName)) || '—',
      exceedCount,
      exceedLabel: belowIsBreach ? 'مدن دون الحد' : 'مدن تتجاوز الحد',
    };
  }
}
