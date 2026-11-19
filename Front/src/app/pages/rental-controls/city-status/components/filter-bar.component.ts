import { Component, EventEmitter, Input, Output } from '@angular/core';
import type { CityData } from '@/models/rental-controls';
import { PeriodFilterComponent } from '@/components/feature/period-filter/period-filter.component';

const statuses = ['الكل', 'الضوابط مفعلة', 'يتطلب دراسة تفصيلية', 'يتطلب دراسة استباقية', 'لا إجراء'];
const classifications = ['الكل', 'نمو مستدام', 'نمو مرتفع', 'نمو متسارع'];

// The period group (app-period-filter) chooses the portal period; the other filters narrow the cities shown.
@Component({
  selector: 'app-filter-bar',
  standalone: true,
  imports: [PeriodFilterComponent],
  templateUrl: './filter-bar.component.html',
})
export class FilterBarComponent {
  @Input() selectedStatus = '';
  @Input() selectedClassification = '';
  @Input() selectedCity = '';
  @Input() cities: CityData[] = [];

  @Output() statusChange = new EventEmitter<string>();
  @Output() classificationChange = new EventEmitter<string>();
  @Output() cityChange = new EventEmitter<string>();
  @Output() reset = new EventEmitter<void>();

  statuses = statuses;
  classifications = classifications;

  selectBaseClass = 'appearance-none bg-white border border-[#E7ECF2] rounded-xl px-4 py-2.5 text-sm text-[#0F172A] pr-9 cursor-pointer focus:outline-none focus:border-[#00848D] focus:ring-1 focus:ring-[#00848D]/20 transition-all duration-200';

  get hasFilters(): boolean {
    return this.selectedStatus !== 'الكل' || this.selectedClassification !== 'الكل' || this.selectedCity !== 'الكل';
  }

  filterOut(list: string[], exclude: string): string[] {
    return list.filter((x) => x !== exclude);
  }
}
