import {
  Component,
  Input,
  Output,
  EventEmitter,
  ViewChild,
  ElementRef,
  HostListener,
  signal,
  computed,
  input,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { CityListItem, CityAnalysisData } from '@/models/rental-controls';
import { PeriodLabelComponent } from '@/components/feature/period-label/period-label.component';

@Component({
  selector: 'app-city-analysis-header',
  standalone: true,
  imports: [CommonModule, PeriodLabelComponent],
  templateUrl: './city-analysis-header.component.html',
})
export class CityAnalysisHeaderComponent {
  @Input({ required: true }) data!: CityAnalysisData;
  // A signal input: the list (and each city's status) changes with the selected period.
  readonly cities = input<CityListItem[]>([]);
  @Output() cityChange = new EventEmitter<string>();

  @ViewChild('dropdownRef') dropdownRef?: ElementRef<HTMLElement>;
  @ViewChild('searchInput') searchInput?: ElementRef<HTMLInputElement>;

  dropdownOpen = signal(false);
  searchQuery = signal('');

  filteredCities = computed<CityListItem[]>(() => {
    const q = this.searchQuery().trim();
    return q ? this.cities().filter((c) => c.cityName.includes(q)) : this.cities();
  });

  readonly statusBgMap: Record<string, string> = {
    'الضوابط مفعلة': '#FDEFEE',
    'يتطلب دراسة تفصيلية': '#FFFBEB',
    'يتطلب دراسة استباقية': '#F0FDFA',
    'لا إجراء': '#EBF7F4',
    'قياس الأثر': '#F1F5F9',
    'تقييم الإيقاف': '#F8FAFC',
  };

  readonly statusTextMap: Record<string, string> = {
    'الضوابط مفعلة': '#DC2626',
    'يتطلب دراسة تفصيلية': '#D97706',
    'يتطلب دراسة استباقية': '#0F766E',
    'لا إجراء': '#0F766E',
    'قياس الأثر': '#002F56',
    'تقييم الإيقاف': '#475569',
  };

  readonly statusDotMap: Record<string, string> = {
    'الضوابط مفعلة': '#DC2626',
    'يتطلب دراسة تفصيلية': '#D97706',
    'يتطلب دراسة استباقية': '#0F766E',
    'لا إجراء': '#0D9488',
    'قياس الأثر': '#005257',
    'تقييم الإيقاف': '#64748B',
  };

  toggleDropdown(): void {
    const next = !this.dropdownOpen();
    this.dropdownOpen.set(next);
    if (next) {
      this.searchQuery.set('');
      setTimeout(() => this.searchInput?.nativeElement.focus(), 50);
    }
  }

  selectCity(cityId: string): void {
    this.cityChange.emit(cityId);
    this.dropdownOpen.set(false);
    this.searchQuery.set('');
  }

  onSearchInput(value: string): void {
    this.searchQuery.set(value);
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (!this.dropdownOpen()) return;
    if (this.dropdownRef && !this.dropdownRef.nativeElement.contains(event.target as Node)) {
      this.dropdownOpen.set(false);
      this.searchQuery.set('');
    }
  }

  @HostListener('document:keydown', ['$event'])
  onDocumentKeyDown(event: KeyboardEvent): void {
    if (this.dropdownOpen() && event.key === 'Escape') {
      this.dropdownOpen.set(false);
      this.searchQuery.set('');
    }
  }
}
