import { Component, Input, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { CityMarketMetric, MarketIndicatorDef } from '@/models/rental-controls';

@Component({
  selector: 'app-indicator-explorer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './indicator-explorer.component.html',
})
export class IndicatorExplorerComponent {
  @Input({ required: true }) set indicators(value: MarketIndicatorDef[]) {
    this._indicators.set(value);
    if (value.length && !value.some((i) => i.id === this.activeTab())) {
      this.activeTab.set(value[0].id);
    }
  }
  @Input({ required: true }) cities: CityMarketMetric[] = [];

  private _indicators = signal<MarketIndicatorDef[]>([]);
  readonly activeTab = signal('rentalTransactionCount');

  get indicatorDefinitions(): MarketIndicatorDef[] {
    return this._indicators();
  }

  readonly activeIndicator = computed(
    () => this._indicators().find((ind) => ind.id === this.activeTab()) || this._indicators()[0],
  );

  sortedCities(): CityMarketMetric[] {
    const key = this.key();
    return this.cities
      .filter((c) => c[key] != null)
      .sort((a, b) => (b[key] as number) - (a[key] as number));
  }

  private total(): number {
    const key = this.key();
    return this.cities.reduce((sum, c) => sum + (c[key] != null ? (c[key] as number) : 0), 0);
  }

  maxVal(): number {
    const key = this.key();
    const vals = this.sortedCities().map((c) => c[key] as number);
    return vals.length ? Math.max(...vals) : 1;
  }

  constructor(private router: Router) {}

  private key(): keyof CityMarketMetric {
    return (this.activeIndicator()?.id ?? 'rentalTransactionCount') as keyof CityMarketMetric;
  }

  setActiveTab(id: string): void {
    this.activeTab.set(id);
  }

  navigateToCity(cityId: string): void {
    this.router.navigate(['/rental-indicators/city-analysis'], { queryParams: { cityId } });
  }

  getVal(city: CityMarketMetric): number {
    return city[this.key()] as number;
  }

  // Additive transaction counts are shown as each city's market share out of 100%.
  private readonly shareIndicators = new Set(['rentalTransactionCount', 'salesTransactionCount']);

  private isShareIndicator(): boolean {
    return this.shareIndicators.has(this.key() as string);
  }

  // Each city's share out of 100 (its value as a percentage of all cities' total).
  getShare(city: CityMarketMetric): number {
    const total = this.total();
    return total > 0 ? (this.getVal(city) / total) * 100 : 0;
  }

  getPct(city: CityMarketMetric): number {
    if (this.isShareIndicator()) return this.getShare(city);
    const max = this.maxVal();
    return max > 0 ? (this.getVal(city) / (max * 1.15)) * 100 : 0;
  }

  getDisplayVal(city: CityMarketMetric): string {
    if (this.isShareIndicator()) return `${this.stripZero(this.getShare(city).toFixed(1))}%`;
    const ind = this.activeIndicator();
    const val = this.getVal(city);
    if (ind.isPercentage) return `${this.stripZero(val.toFixed(1))}%`;
    const unit = (ind.unit || '').trim();
    if (unit === 'ريال') return this.formatRiyal(val);
    if (unit === 'ألف') return `${this.stripZero(val.toFixed(1))} ألف`; // value already in thousands
    return this.stripZero(val.toFixed(1)); // index / plain number
  }

  // Abbreviate ريال values to ألف/مليون; currency word omitted (already in the chart title).
  private formatRiyal(val: number): string {
    const abs = Math.abs(val);
    if (abs >= 1_000_000) return `${this.stripZero((val / 1_000_000).toFixed(2))} مليون`;
    if (abs >= 1_000) return `${this.stripZero((val / 1_000).toFixed(1))} ألف`;
    return `${Math.round(val)}`;
  }

  private stripZero(s: string): string {
    return s.replace(/\.0+$/, '');
  }

  getBarColor(city: CityMarketMetric, idx: number): string {
    // Top performer highlighted for positive indicators; all others teal. (Thresholds removed.)
    return this.activeIndicator()?.higherIs === 'positive' && idx === 0 ? '#059669' : '#00848D';
  }
}
