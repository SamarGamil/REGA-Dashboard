import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LayoutComponent } from '@/components/feature/layout/layout.component';
import { ReportsService } from '@/services/reports.service';
import { RentalControlsService } from '@/services/rental-controls.service';
import { CityListItem } from '@/models/rental-controls';
import { ReportFormat, ReportHistoryItem, ReportPeriodOption, ReportPeriods, ReportPeriodType } from '@/models/reports';
import { pageSlice, pageWindow } from '@/utils/pagination';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, LayoutComponent],
  templateUrl: './reports.component.html',
})
export class ReportsComponent implements OnInit {
  private svc = inject(ReportsService);
  private rentalControls = inject(RentalControlsService);

  readonly periodTypes: { id: ReportPeriodType; label: string }[] = [
    { id: 'monthly', label: 'شهري' },
    { id: 'quarterly', label: 'ربع سنوي' },
    { id: 'yearly', label: 'سنوي' },
  ];

  reportType = signal<'national' | 'city'>('national');
  periodType = signal<ReportPeriodType>('quarterly');
  periods = signal<ReportPeriods>({ current: '', monthly: [], quarterly: [] });
  selectedPeriodId = signal<string>('');
  format = signal<ReportFormat>('pdf');
  generating = signal(false);
  history = signal<ReportHistoryItem[]>([]);
  cities = signal<CityListItem[]>([]);
  selectedCityId = signal<string>('');

  /** Periods with stored data for the selected period type, newest first. */
  readonly periodOptions = computed<ReportPeriodOption[]>(() => this.optionsFor(this.periodType()));
  readonly selectedPeriod = computed(() => this.periodOptions().find((p) => p.id === this.selectedPeriodId()));

  readonly canGenerate = computed(() =>
    !this.generating() && !!this.selectedPeriod() && (this.reportType() === 'national' || !!this.selectedCityId()));

  // Client-side paging of the report history (newest first).
  readonly pageSizes = [10, 25, 50];
  historyPage = signal(1);
  historyPageSize = signal(10);
  readonly historySlice = computed(() => pageSlice(this.history().length, this.historyPage(), this.historyPageSize()));
  readonly pagedHistory = computed(() => {
    const { start, end } = this.historySlice();
    return this.history().slice(start, end);
  });
  readonly historyPages = computed(() => pageWindow(this.historySlice().page, this.historySlice().totalPages));

  goToHistoryPage(page: number) {
    this.historyPage.set(pageSlice(this.history().length, page, this.historyPageSize()).page);
  }

  onHistoryPageSizeChange(event: Event) {
    this.historyPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.historyPage.set(1);
  }

  ngOnInit() {
    this.loadHistory();
    this.svc.getPeriods().subscribe({
      next: (periods) => {
        this.periods.set(periods);
        this.selectedPeriodId.set(
          periods.quarterly.find((p) => p.id === periods.current)?.id ?? periods.quarterly[0]?.id ?? '');
      },
      error: () => {},
    });
    this.rentalControls.getCities().subscribe({
      next: (items) => {
        this.cities.set(items);
        if (!this.selectedCityId() && items.length > 0) this.selectedCityId.set(items[0].cityId);
      },
      error: () => {},
    });
  }

  isPeriodTypeAvailable(type: ReportPeriodType): boolean {
    return this.optionsFor(type).length > 0;
  }

  setPeriodType(type: ReportPeriodType) {
    if (!this.isPeriodTypeAvailable(type)) return;
    this.periodType.set(type);
    this.selectedPeriodId.set(this.optionsFor(type)[0]?.id ?? '');
  }

  onPeriodChange(event: Event) {
    this.selectedPeriodId.set((event.target as HTMLSelectElement).value);
  }

  private optionsFor(type: ReportPeriodType): ReportPeriodOption[] {
    const p = this.periods();
    return type === 'monthly' ? p.monthly : type === 'quarterly' ? p.quarterly : [];
  }

  private loadHistory() {
    this.svc.getHistory().subscribe({
      next: (items) => this.history.set(items),
      error: () => {},
    });
  }

  setFormat(f: ReportFormat) { this.format.set(f); }

  onCityChange(event: Event) {
    this.selectedCityId.set((event.target as HTMLSelectElement).value);
  }

  generate() {
    if (!this.canGenerate()) return;
    this.generating.set(true);
    const fmt = this.format();
    const isCity = this.reportType() === 'city';
    const cityId = this.selectedCityId();
    const city = this.cities().find((c) => c.cityId === cityId);
    const period = this.selectedPeriod()!;
    const request$ = isCity
      ? this.svc.generateCity(cityId, fmt, true, period.id)
      : this.svc.generateNational(fmt, true, period.id);

    request$.subscribe({
      next: ({ blob, fileName }) => {
        const url = URL.createObjectURL(blob);
        this.saveBlob(url, fileName);
        this.history.update((h) => [
          {
            id: crypto.randomUUID(),
            name: isCity ? `تقرير الضوابط الإيجارية – ${city?.cityName ?? cityId}` : 'تقرير الضوابط الإيجارية الوطني',
            type: isCity ? 'تقرير مدينة' : 'التقرير الوطني',
            scope: isCity ? (city?.cityName ?? cityId) : 'جميع المدن',
            period: period.label,
            periodId: period.id,
            format: fmt,
            date: new Date().toLocaleDateString('ar-SA'),
            status: 'ready',
            blobUrl: url,
            fileName,
            cityId: isCity ? cityId : undefined,
          },
          ...h,
        ]);
        this.historyPage.set(1); // the new report is listed first
        this.generating.set(false);
      },
      error: () => this.generating.set(false),
    });
  }

  redownload(item: ReportHistoryItem) {
    if (item.blobUrl && item.fileName) {
      this.saveBlob(item.blobUrl, item.fileName);
      return;
    }
    // Past-session items have no cached blob — regenerate the report on demand (without logging a new entry).
    // Items recorded before period selection have no periodId and regenerate for the current quarter.
    if (item.type === 'تقرير مدينة') {
      const cityId = item.cityId ?? this.cities().find((c) => c.cityName === item.scope)?.cityId;
      if (!cityId) return;
      this.svc.generateCity(cityId, item.format, false, item.periodId).subscribe({
        next: ({ blob, fileName }) => this.saveBlob(URL.createObjectURL(blob), fileName),
        error: () => {},
      });
      return;
    }
    this.svc.generateNational(item.format, false, item.periodId).subscribe({
      next: ({ blob, fileName }) => this.saveBlob(URL.createObjectURL(blob), fileName),
      error: () => {},
    });
  }

  private saveBlob(url: string, fileName: string) {
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
  }
}
