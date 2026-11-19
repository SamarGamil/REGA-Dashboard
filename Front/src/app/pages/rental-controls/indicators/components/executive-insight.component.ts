import { Component, Input, OnChanges } from '@angular/core';
import { HistoricalCityRow, HistoricalIndicatorData } from '@/models/rental-controls';
import { extremeCities, firstYearWithData, joinCityNames, largestChangeCities } from './historical-summary.utils';

@Component({
  selector: 'app-executive-insight',
  standalone: true,
  template: `
    <div
      class="p-3 rounded-lg mt-4 flex items-start gap-2.5"
      style="background-color: #F0F9FA; border: 1px solid #C8E6E3"
    >
      <div class="w-5 h-5 flex items-center justify-center flex-shrink-0 mt-0.5">
        <i class="ri-lightbulb-line text-[#00848D] text-sm"></i>
      </div>
      <div>
        <p class="text-sm font-bold text-[#00848D] mb-0.5">الخلاصة التنفيذية</p>
        <p class="text-sm text-[#0F2137] leading-relaxed">{{ insight }}</p>
      </div>
    </div>
  `,
})
export class ExecutiveInsightComponent implements OnChanges {
  @Input({ required: true }) data!: HistoricalIndicatorData;

  insight = '';

  ngOnChanges(): void {
    this.insight = this.computeInsight(this.data);
  }

  private computeInsight(data: HistoricalIndicatorData): string {
    const { cities, years } = data;
    const lastYear = years[years.length - 1];
    // Vacancy/burden history starts later than the tab's first column; name the year data actually starts.
    const startYear = firstYearWithData(cities, years) ?? years[0];

    const names = (rows: HistoricalCityRow[]) => joinCityNames(rows.map((c) => c.cityName));
    const top = extremeCities(cities, lastYear, 'max');
    const bottom = extremeCities(cities, lastYear, 'min');
    const changed = largestChangeCities(cities, years);

    const parts: string[] = [];
    if (top.length) parts.push(`تسجل ${names(top)} أعلى قيمة حالية (${top[0].displayValues[lastYear]})`);
    if (bottom.length) parts.push(`بينما تسجل ${names(bottom)} أدنى قيمة (${bottom[0].displayValues[lastYear]})`);
    let text = parts.join('، ');
    if (changed.length) text += `${text ? '. ' : ''}وشهدت ${names(changed)} أكبر تغير مقارنة ببداية الفترة (${startYear})`;
    return text ? `${text}.` : '';
  }
}
