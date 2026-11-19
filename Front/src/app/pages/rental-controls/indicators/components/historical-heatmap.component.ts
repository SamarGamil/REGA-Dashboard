import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HistoricalCityRow, HistoricalIndicatorData } from '@/models/rental-controls';

type SortCol = 'current' | 'change' | 'city';
type SortDir = 'asc' | 'desc';

@Component({
  selector: 'app-historical-heatmap',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './historical-heatmap.component.html',
})
export class HistoricalHeatmapComponent {
  @Input({ required: true }) data!: HistoricalIndicatorData;

  readonly selectedCity = signal<string | null>(null);
  readonly sortBy = signal<SortCol>('current');
  readonly sortDir = signal<SortDir>('desc');

  get years(): string[] {
    return this.data.years;
  }

  get lastYear(): string {
    return this.data.years[this.data.years.length - 1];
  }

  handleSort(col: SortCol): void {
    if (this.sortBy() === col) {
      this.sortDir.set(this.sortDir() === 'desc' ? 'asc' : 'desc');
    } else {
      this.sortBy.set(col);
      this.sortDir.set('desc');
    }
  }

  get sortedCities(): HistoricalCityRow[] {
    const { years, cities } = this.data;
    const lastYear = this.lastYear;
    const sortBy = this.sortBy();
    const sortDir = this.sortDir();
    // Change over the full window only when both endpoints exist (matches the "—" display).
    const changeOf = (c: HistoricalCityRow): number | null => {
      const first = c.values[years[0]];
      const last = c.values[lastYear];
      return first == null || last == null ? null : last - first;
    };
    // No-data rows always sink to the bottom, regardless of sort direction.
    const nullLast = (a: number | null, b: number | null, cmp: number): number => {
      if (a == null && b == null) return 0;
      if (a == null) return 1;
      if (b == null) return -1;
      return cmp;
    };
    return [...cities].sort((a, b) => {
      if (sortBy === 'city') {
        return sortDir === 'asc'
          ? a.cityName.localeCompare(b.cityName, 'ar')
          : b.cityName.localeCompare(a.cityName, 'ar');
      }
      if (sortBy === 'current') {
        const va = a.values[lastYear];
        const vb = b.values[lastYear];
        return nullLast(va, vb, sortDir === 'desc' ? (vb as number) - (va as number) : (va as number) - (vb as number));
      }
      const ca = changeOf(a);
      const cb = changeOf(b);
      return nullLast(ca, cb, sortDir === 'desc' ? (cb as number) - (ca as number) : (ca as number) - (cb as number));
    });
  }

  getHeatCellClass(value: number | null): string {
    if (value === null) return 'bg-transparent';
    for (const t of this.data.thresholds) {
      if (value >= t.min && value < t.max) {
        return t.cssClass.split(' ')[0];
      }
    }
    return 'bg-transparent';
  }

  getCellTextClass(value: number | null): string {
    if (value === null) return 'text-[#4B5563]';
    for (const t of this.data.thresholds) {
      if (value >= t.min && value < t.max) {
        return t.cssClass.split(' ')[1];
      }
    }
    return 'text-[#0F2137]';
  }

  computeChange(city: HistoricalCityRow): { text: string; direction: 'up' | 'down' | 'stable' } {
    const years = this.years;
    const first = city.values[years[0]];
    const last = city.values[years[years.length - 1]];
    if (first === null || last === null || first === undefined || last === undefined) {
      return { text: '—', direction: 'stable' };
    }
    // Round to 2 dp first so float noise (e.g. 8.79-8.69 = 0.0999…) doesn't misfire the < 0.1 threshold.
    const diff = Math.round((last - first) * 100) / 100;
    if (Math.abs(diff) < 0.1) return { text: 'مستقر', direction: 'stable' };
    const sign = diff > 0 ? '+' : '';
    const isPercentage = city.displayValues[years[0]].includes('%');
    const text = isPercentage
      ? `${sign}${diff.toFixed(1)} نقطة مئوية`
      : `${sign}${Math.round(diff).toLocaleString('ar-SA')}`;
    return { text, direction: diff > 0 ? 'up' : 'down' };
  }

  toggleCity(cityName: string): void {
    this.selectedCity.set(this.selectedCity() === cityName ? null : cityName);
  }
}
