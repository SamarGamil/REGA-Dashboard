import { Component, Input, OnChanges, computed, signal } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';
import { SupplyDemandGapCity } from '@/models/rental-controls';
import { applyRtl } from '@/utils/chart-theme';
import { IndicatorTabsComponent } from './indicator-tabs.component';
import { ChartDirectionArrowComponent } from '@/components/base/chart-direction-arrow/chart-direction-arrow.component';
import { GAP_COLORS, buildAnnualGapOption, buildMonthlyGapOption, formatUnits } from './gap-chart-options';
import { DEFAULT_GAP_MODE, GapMode, buildGapInsights, gapKindLabel, gapRangeLabel } from './gap-insights.utils';

interface GapKpiCard {
  title: string;
  kind: 'expected' | 'actual';
  gap: number | null;
  note: string;
}

@Component({
  selector: 'app-supply-demand-gap',
  standalone: true,
  imports: [NgxEchartsDirective, IndicatorTabsComponent, ChartDirectionArrowComponent],
  templateUrl: './supply-demand-gap.component.html',
})
export class SupplyDemandGapComponent implements OnChanges {
  @Input({ required: true }) cities: SupplyDemandGapCity[] = [];

  readonly colors = GAP_COLORS;
  readonly formatUnits = formatUnits;
  readonly gapKindLabel = gapKindLabel;

  private readonly citiesSig = signal<SupplyDemandGapCity[]>([]);
  readonly activeCityId = signal('');
  readonly mode = signal<GapMode>(DEFAULT_GAP_MODE);

  readonly tabs = computed(() => this.citiesSig().map((c) => ({ id: c.cityId, title: c.cityName })));
  readonly city = computed<SupplyDemandGapCity | null>(
    () => this.citiesSig().find((c) => c.cityId === this.activeCityId()) ?? this.citiesSig()[0] ?? null,
  );
  readonly lastYear = computed(() => {
    const years = this.citiesSig().flatMap((c) => c.annual.map((a) => a.year));
    return years.length ? Math.max(...years) : null;
  });

  readonly chartOption = computed<EChartsOption | null>(() => {
    const c = this.city();
    if (!c) return null;
    return applyRtl(this.mode() === 'annual' ? buildAnnualGapOption(c.annual) : buildMonthlyGapOption(c.monthly));
  });

  readonly insights = computed(() => {
    const c = this.city();
    return c ? buildGapInsights(c, this.mode()) : [];
  });

  readonly rangeLabel = computed(() => {
    const c = this.city();
    return c ? gapRangeLabel(c, this.mode()) : '';
  });

  readonly kpiCards = computed<GapKpiCard[]>(() => {
    const c = this.city();
    if (!c) return [];
    const k = c.kpis;
    const assumption = c.vacancyAssumptionPct != null ? ` وفق افتراض شواغر ${c.vacancyAssumptionPct}%` : '';
    const yearCard = (y: typeof k.firstYear, fallbackTitle: string): GapKpiCard => {
      if (y && y.actualGap != null) {
        return {
          title: `الفجوة الفعلية ${y.year}`,
          kind: 'actual',
          gap: y.actualGap,
          note: `المتوقع ${formatUnits(y.gap)} وحدة (${gapKindLabel(y.gap)})`,
        };
      }
      return {
        title: y ? `الفجوة المتوقعة ${y.year}` : fallbackTitle,
        kind: 'expected',
        gap: y?.gap ?? null,
        note: y ? `تقدير سنوي لعام ${y.year}${assumption}` : '',
      };
    };
    return [
      yearCard(k.firstYear, 'الفجوة المتوقعة'),
      yearCard(k.secondYear, 'الفجوة المتوقعة'),
      {
        title: 'الفجوة المتوقعة للشهر',
        kind: 'expected',
        gap: k.monthExpected?.gap ?? null,
        note: k.monthExpected ? `${k.monthExpected.label} — قيمة شهرية متوقعة` : '',
      },
      {
        title: 'الفجوة الفعلية للشهر',
        kind: 'actual',
        gap: k.monthActual?.gap ?? null,
        note: k.monthActual ? `${k.monthActual.label} — بيانات فعلية` : '',
      },
    ];
  });

  ngOnChanges(): void {
    this.citiesSig.set(this.cities);
    if (!this.cities.some((c) => c.cityId === this.activeCityId())) {
      this.activeCityId.set(this.cities[0]?.cityId ?? '');
    }
  }
}
