import { Component, Input, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import type { CityData } from '@/models/rental-controls';
import { symbolicReference } from '@/utils/indicator-labels';

type KpiStatus = 'normal' | 'attention' | 'critical';
type ArrowDirection = 'up' | 'down';

interface KpiState {
  status: KpiStatus;
  label: string | null;
  reference: string;
  tooltipLabel: string;
  arrowDirection: ArrowDirection;
}

interface KpiItem {
  label: string;
  value: string;
  state: KpiState;
}

function parsePercent(value: string): number | null {
  const cleaned = value.replace('%', '').replace('+', '').replace('−', '-').trim();
  const n = parseFloat(cleaned);
  return Number.isNaN(n) ? null : n;
}

function getHousingBurdenStatus(value: string): KpiState {
  const ref = '30%';
  const num = parsePercent(value);
  if (num === null) return { status: 'normal', label: null, reference: ref, tooltipLabel: 'العبء السكني', arrowDirection: 'up' };
  let status: KpiStatus = 'normal';
  if (num > 35) { status = 'critical'; }
  else if (num >= 31) { status = 'attention'; }
  return { status, label: null, reference: ref, tooltipLabel: 'العبء السكني', arrowDirection: 'up' };
}

function getInvestmentReturnStatus(value: string): KpiState {
  const ref = `${symbolicReference('أقل من 6%')} حد مرجعي`;
  const num = parsePercent(value);
  if (num === null) return { status: 'normal', label: null, reference: ref, tooltipLabel: 'العائد الإيجاري', arrowDirection: 'down' };
  let status: KpiStatus = 'normal';
  if (num < 4) { status = 'critical'; }
  else if (num < 6) { status = 'attention'; }
  return { status, label: null, reference: ref, tooltipLabel: 'العائد الإيجاري', arrowDirection: 'down' };
}

function getRentalGrowthStatus(value: string): KpiState {
  const ref = '≤5% معدل صحي';
  const num = parsePercent(value);
  if (num === null) return { status: 'normal', label: null, reference: ref, tooltipLabel: 'الزيادة الإيجارية', arrowDirection: 'up' };
  let status: KpiStatus = 'normal';
  if (num > 8) { status = 'critical'; }
  else if (num > 5) { status = 'attention'; }
  return { status, label: null, reference: ref, tooltipLabel: 'الزيادة الإيجارية', arrowDirection: 'up' };
}

function getRentToIncomeStatus(value: string): KpiState {
  const ref = '≤30% حد مرجعي';
  const num = parsePercent(value);
  if (num === null) return { status: 'normal', label: null, reference: ref, tooltipLabel: 'نسبة الإيجار إلى الدخل', arrowDirection: 'up' };
  let status: KpiStatus = 'normal';
  if (num > 40) { status = 'critical'; }
  else if (num > 30) { status = 'attention'; }
  return { status, label: null, reference: ref, tooltipLabel: 'نسبة الإيجار إلى الدخل', arrowDirection: 'up' };
}

const statusStrokeMap: Record<string, string> = {
  'الضوابط مفعلة': '#E5484D',
  'يتطلب دراسة تفصيلية': '#F59E0B',
  'يتطلب دراسة استباقية': '#0D9488',
  'لا إجراء': '#0D9488',
};

const statusBadgeBgMap: Record<string, string> = {
  'الضوابط مفعلة': '#FDEFEE',
  'يتطلب دراسة تفصيلية': '#FDF5E8',
  'يتطلب دراسة استباقية': '#EBF7F4',
  'لا إجراء': '#EBF7F4',
};

const statusBadgeTextMap: Record<string, string> = {
  'الضوابط مفعلة': '#DC2626',
  'يتطلب دراسة تفصيلية': '#D97706',
  'يتطلب دراسة استباقية': '#0F766E',
  'لا إجراء': '#0F766E',
};

const statusBadgeBorderMap: Record<string, string> = {
  'الضوابط مفعلة': '#FECACA',
  'يتطلب دراسة تفصيلية': '#FDE68A',
  'يتطلب دراسة استباقية': '#99F6E4',
  'لا إجراء': '#99F6E4',
};

// Card body follows the growth classification so it matches the legend; the badge carries the status.
const classificationPalette: Record<string, { stroke: string; bg: string; text: string }> = {
  sustainable: { stroke: '#0D9488', bg: '#EBF7F4', text: '#0F766E' },
  elevated: { stroke: '#F59E0B', bg: '#FDF5E8', text: '#D97706' },
  accelerated: { stroke: '#E5484D', bg: '#FDEFEE', text: '#DC2626' },
};

const kpiAlertValueColor: Record<KpiStatus, string> = {
  normal: '#0F172A',
  attention: '#DC2626',
  critical: '#DC2626',
};

@Component({
  selector: 'app-city-card',
  standalone: true,
  templateUrl: './city-card.component.html',
  host: { class: 'block h-full' },
})
export class CityCardComponent {
  // Kept in a signal so kpiItems recomputes when the grid passes this card a new period's data for the same city.
  private readonly cityData = signal<CityData | null>(null);
  @Input({ required: true }) set city(value: CityData) { this.cityData.set(value); }
  get city(): CityData { return this.cityData()!; }

  /** The period the CAGR figure belongs to (the page's portal period), stated on the card. */
  @Input() periodLabel = '';

  hoveredKpi = signal<string | null>(null);
  isHovered = signal(false);

  constructor(private router: Router) {}

  get strokeColor(): string { return classificationPalette[this.city.classificationLabel]?.stroke || statusStrokeMap[this.city.operationalStatus] || '#94A3B8'; }
  get cardBg(): string { return classificationPalette[this.city.classificationLabel]?.bg || this.badgeBg; }
  get cardText(): string { return classificationPalette[this.city.classificationLabel]?.text || this.badgeText; }
  get badgeBg(): string { return statusBadgeBgMap[this.city.operationalStatus] || '#F8FAFC'; }
  get badgeText(): string { return statusBadgeTextMap[this.city.operationalStatus] || '#475569'; }
  get badgeBorder(): string { return statusBadgeBorderMap[this.city.operationalStatus] || '#E2E8F0'; }

  get isHighGrowth(): boolean {
    return this.city.classificationLabel === 'elevated' || this.city.classificationLabel === 'accelerated';
  }

  get cardShadow(): string {
    return this.isHovered()
      ? '0 4px 24px rgba(15,23,42,0.08), 0 1px 4px rgba(15,23,42,0.04)'
      : '0 1px 4px rgba(15,23,42,0.04), 0 1px 2px rgba(15,23,42,0.02)';
  }

  kpiItems = computed<KpiItem[]>(() => {
    const city = this.city;
    const burdenState = getHousingBurdenStatus(city.housingBurden);
    const growthState = getRentalGrowthStatus(city.actualRentalGrowth);
    const investState = getInvestmentReturnStatus(city.investmentReturn);
    const rentToIncomeState = getRentToIncomeStatus(city.rentToIncome);

    if (this.isHighGrowth) {
      return [
        { label: 'الزيادة الإيجارية', value: city.actualRentalGrowth, state: growthState },
        { label: 'نسبة الإيجار للدخل', value: city.rentToIncome, state: rentToIncomeState },
        { label: 'العبء السكني', value: city.housingBurden, state: burdenState },
      ];
    }
    return [
      { label: 'الزيادة الإيجارية', value: city.actualRentalGrowth, state: growthState },
      { label: 'العائد الإيجاري', value: city.investmentReturn, state: investState },
      { label: 'العبء السكني', value: city.housingBurden, state: burdenState },
    ];
  });

  valueColor(state: KpiState): string { return kpiAlertValueColor[state.status]; }
  isAbnormal(state: KpiState): boolean { return state.status !== 'normal'; }
  showTooltip(state: KpiState): boolean {
    return this.hoveredKpi() === state.tooltipLabel && state.status !== 'normal';
  }

  handleClick(): void {
    this.router.navigate(['/rental-indicators/city-analysis'], { queryParams: { city: this.city.id } });
  }

  handleKeyDown(e: KeyboardEvent): void {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      this.handleClick();
    }
  }
}
