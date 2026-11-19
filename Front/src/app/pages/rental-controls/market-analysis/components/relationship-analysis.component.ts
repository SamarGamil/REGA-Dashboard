import { Component, Input, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { CityMarketMetric, MarketIndicatorDef } from '@/models/rental-controls';

interface CityWithPosition extends CityMarketMetric {
  x: number;
  y: number;
  valH: number;
  valV: number;
}
interface QuadrantZoneLabel { topRight: string; topLeft: string; bottomRight: string; bottomLeft: string; }
interface RelationshipInsightItem { city: string; text: string; }

function mapToPosition(val: number, min: number, max: number, threshold: number): number {
  const pad = 10;
  if (val <= threshold) {
    const range = threshold - min || 1;
    return pad + ((val - min) / range) * (50 - pad);
  }
  const range = max - threshold || 1;
  return 50 + ((val - threshold) / range) * (50 - pad);
}

function zoneLabelsFor(hId: string, vId: string): QuadrantZoneLabel {
  if (hId === 'rentalTransactionCount' && vId === 'avgRentalTransactionValue')
    return { topRight: 'نشط مرتفع القيمة', topLeft: 'قيمة بلا حجم', bottomRight: 'حجم بقيمة منخفضة', bottomLeft: 'سوق هادئ' };
  if (hId === 'salesTransactionCount' && vId === 'avgSalesTransactionValue')
    return { topRight: 'سوق بيعي نشط', topLeft: 'صفقات محدودة مرتفعة', bottomRight: 'نشاط بيعي متوسط', bottomLeft: 'سوق بيعي هادئ' };
  if (hId === 'actualRentCpi' && vId === 'generalInflationCpi')
    return { topRight: 'ضغط تضخمي مركب', topLeft: 'ضغط تضخمي عام', bottomRight: 'ضغط إيجاري منعزل', bottomLeft: 'استقرار' };
  if (hId === 'apartmentRentIndex' && vId === 'villaRentIndex')
    return { topRight: 'ارتفاع شامل', topLeft: 'ارتفاع الفيلات', bottomRight: 'ارتفاع الشقق', bottomLeft: 'استقرار' };
  return { topRight: 'ضغط مرتفع', topLeft: 'تنبيه', bottomRight: 'متابعة', bottomLeft: 'مستقر' };
}

function quadrantInsights(
  hId: keyof CityMarketMetric, vId: keyof CityMarketMetric, cities: CityMarketMetric[],
  hThreshold: number, vThreshold: number, indicators: MarketIndicatorDef[],
): RelationshipInsightItem[] {
  const hLabel = indicators.find((d) => d.id === hId)?.label || hId;
  const vLabel = indicators.find((d) => d.id === vId)?.label || vId;
  const valid = cities.filter((c) => c[hId] != null && c[vId] != null);
  const highBoth = valid.filter((c) => (c[hId] as number) >= hThreshold && (c[vId] as number) >= vThreshold);
  const lowBoth = valid.filter((c) => (c[hId] as number) < hThreshold && (c[vId] as number) < vThreshold);
  const insights: RelationshipInsightItem[] = [];
  if (highBoth.length > 0) {
    insights.push({ city: highBoth.map((c) => c.name).join('، '), text: `تقع ضمن منطقة الضغط المرتفع نتيجة اجتماع ${hLabel} مرتفع مع ${vLabel} أعلى من الحد المرجعي.` });
  }
  const sortedV = [...valid].sort((a, b) => (b[vId] as number) - (a[vId] as number));
  if (sortedV.length > 0 && !highBoth.some((c) => c.id === sortedV[0].id)) {
    insights.push({ city: sortedV[0].name, text: `تسجل أعلى ${vLabel} بين المدن المختارة.` });
  }
  if (lowBoth.length > 0 && lowBoth.length <= 3) {
    insights.push({ city: lowBoth.map((c) => c.name).join('، '), text: `تقع ضمن نطاق أكثر استقراراً مقارنة بالمدن الأخرى.` });
  } else if (lowBoth.length > 3) {
    const sample = lowBoth.slice(0, 2).map((c) => c.name).join(' و');
    insights.push({ city: sample, text: `ضمن نطاق الاستقرار مع ${hLabel} و${vLabel} أقل من الحدود المرجعية.` });
  }
  return insights.slice(0, 3);
}

@Component({
  selector: 'app-relationship-analysis',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './relationship-analysis.component.html',
})
export class RelationshipAnalysisComponent {
  @Input({ required: true }) cities: CityMarketMetric[] = [];
  @Input({ required: true }) set indicators(value: MarketIndicatorDef[]) {
    this._indicators.set(value);
  }
  private _indicators = signal<MarketIndicatorDef[]>([]);

  readonly hId = signal('rentalTransactionCount');
  readonly vId = signal('avgRentalTransactionValue');

  readonly hIndicator = computed(() => this._indicators().find((d) => d.id === this.hId()));
  readonly vIndicator = computed(() => this._indicators().find((d) => d.id === this.vId()));
  readonly hThreshold = computed(() => this.hIndicator()?.threshold ?? 5);
  readonly vThreshold = computed(() => this.vIndicator()?.threshold ?? 20000);
  readonly hLabel = computed(() => this.hIndicator()?.label || this.hId());
  readonly vLabel = computed(() => this.vIndicator()?.label || this.vId());
  readonly hUnit = computed(() => (this.hIndicator()?.isPercentage ? '%' : this.hIndicator()?.unit) || '');
  readonly vUnit = computed(() => (this.vIndicator()?.isPercentage ? '%' : this.vIndicator()?.unit) || '');
  readonly zoneLabels = computed(() => zoneLabelsFor(this.hId(), this.vId()));
  readonly availableForH = computed(() => this._indicators().filter((d) => d.id !== this.vId()));
  readonly availableForV = computed(() => this._indicators().filter((d) => d.id !== this.hId()));

  readonly citiesWithPositions = computed<CityWithPosition[]>(() => {
    const keyH = this.hId() as keyof CityMarketMetric;
    const keyV = this.vId() as keyof CityMarketMetric;
    const hT = this.hThreshold();
    const vT = this.vThreshold();
    const valid = this.cities.filter((c) => c[keyH] != null && c[keyV] != null);
    if (!valid.length) return [];
    const valsH = valid.map((c) => c[keyH] as number);
    const valsV = valid.map((c) => c[keyV] as number);
    const minH = Math.min(...valsH), maxH = Math.max(...valsH);
    const minV = Math.min(...valsV), maxV = Math.max(...valsV);
    return valid.map((c) => ({
      ...c,
      x: mapToPosition(c[keyH] as number, minH, maxH, hT),
      y: 100 - mapToPosition(c[keyV] as number, minV, maxV, vT),
      valH: c[keyH] as number,
      valV: c[keyV] as number,
    }));
  });

  readonly quadrantCities = computed(() => {
    const cities = this.citiesWithPositions();
    const hT = this.hThreshold(), vT = this.vThreshold();
    return {
      tr: cities.filter((c) => c.valH >= hT && c.valV >= vT),
      tl: cities.filter((c) => c.valH < hT && c.valV >= vT),
      br: cities.filter((c) => c.valH >= hT && c.valV < vT),
      bl: cities.filter((c) => c.valH < hT && c.valV < vT),
    };
  });

  readonly insights = computed(() =>
    quadrantInsights(this.hId() as keyof CityMarketMetric, this.vId() as keyof CityMarketMetric, this.cities, this.hThreshold(), this.vThreshold(), this._indicators()),
  );

  readonly quadrantColors = computed(() => {
    const z = this.zoneLabels();
    return {
      tl: { bg: 'rgba(254,226,226,0.4)', text: '#B91C1C', label: z.topLeft },
      tr: { bg: 'rgba(254,243,199,0.5)', text: '#B45309', label: z.topRight },
      bl: { bg: 'rgba(209,250,229,0.4)', text: '#047857', label: z.bottomLeft },
      br: { bg: 'rgba(241,245,249,0.6)', text: '#475569', label: z.bottomRight },
    };
  });

  // Highlight (bold label + larger dot) is now data-driven: pressure-zone cities plus each axis leader.
  readonly highlightedIds = computed<Set<string>>(() => {
    const cities = this.citiesWithPositions();
    const ids = new Set<string>();
    if (!cities.length) return ids;
    const hT = this.hThreshold(), vT = this.vThreshold();
    cities.filter((c) => c.valH >= hT && c.valV >= vT).forEach((c) => ids.add(c.id));
    ids.add(cities.reduce((a, b) => (b.valH > a.valH ? b : a)).id);
    ids.add(cities.reduce((a, b) => (b.valV > a.valV ? b : a)).id);
    return ids;
  });

  constructor(private router: Router) {}

  setHId(value: string): void { this.hId.set(value); }
  setVId(value: string): void { this.vId.set(value); }
  isHighlighted(id: string): boolean { return this.highlightedIds().has(id); }
  tooltip(city: CityWithPosition): string {
    return `${city.name} — ${this.hLabel()}: ${city.valH}${this.hUnit()} · ${this.vLabel()}: ${city.valV}${this.vUnit()}`;
  }
  padIndex(idx: number): string { return (idx + 1).toString().padStart(2, '0'); }
  navigateToCity(cityId: string): void {
    this.router.navigate(['/rental-indicators/city-analysis'], { queryParams: { cityId } });
  }
}
