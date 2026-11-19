import type { SupplyDemandGapCity } from '@/models/rental-controls';

// Type-only imports: this file is unit-tested with `node --test`.

export type GapMode = 'annual' | 'monthly';
export const DEFAULT_GAP_MODE: GapMode = 'monthly';

const units = (v: number) => Math.round(Math.abs(v)).toLocaleString('en-US');
const pct = (part: number, whole: number) => (whole === 0 ? 0 : Math.round((part / Math.abs(whole)) * 1000) / 10);

export function gapKindLabel(gap: number): 'عجز' | 'فائض' | 'توازن' {
  return gap < 0 ? 'عجز' : gap > 0 ? 'فائض' : 'توازن';
}

// "العجز" / "الفائض" / "التوازن" — the label with the definite article for sentence starts.
const definite = (gap: number) => `ال${gapKindLabel(gap)}`;

// A percentage change only makes sense between two non-zero gaps on the same side (both shortages or both surpluses).
const comparable = (x: number, y: number) => x !== 0 && y !== 0 && Math.sign(x) === Math.sign(y);
const flipped = (x: number, y: number) => x !== 0 && y !== 0 && Math.sign(x) !== Math.sign(y);
const direction = (from: number, to: number) =>
  Math.abs(to) > Math.abs(from) ? 'تتسع' : Math.abs(to) < Math.abs(from) ? 'تضيق' : 'تستقر';

export function gapRangeLabel(city: SupplyDemandGapCity, mode: GapMode): string {
  if (mode === 'annual') {
    const a = city.annual;
    return a.length ? `${a[0].year}–${a[a.length - 1].year}` : '';
  }
  const m = city.monthly;
  if (!m.length) return '';
  const first = m[0].label.split(' ')[0];
  return `${first} – ${m[m.length - 1].label}`;
}

export function buildGapInsights(city: SupplyDemandGapCity, mode: GapMode): string[] {
  return mode === 'annual' ? annualInsights(city) : monthlyInsights(city);
}

function annualInsights(city: SupplyDemandGapCity): string[] {
  const a = city.annual;
  if (!a.length) return [];
  const first = a[0];
  const last = a[a.length - 1];
  const peak = a.reduce((m, p) => (Math.abs(p.gap) > Math.abs(m.gap) ? p : m), first);

  const expectedNote = `(المتوقع ${units(first.expectedGap)}${comparable(first.gap, first.expectedGap) ? '' : ` ${gapKindLabel(first.expectedGap)}`})`;
  const out = [
    first.isActual
      ? `بلغ ${definite(first.gap)} الفعلي في ${city.cityName} ${units(first.gap)} وحدة سكنية عام ${first.year} ${expectedNote}، مقابل طلب متوقع ${units(first.demand)} وحدة وعرض فعلي ${units(first.supply)} وحدة.`
      : `يبلغ ${definite(first.gap)} المتوقع في ${city.cityName} ${units(first.gap)} وحدة سكنية عام ${first.year}، مقابل طلب متوقع ${units(first.demand)} وحدة وعرض متوقع ${units(first.supply)} وحدة.`,
  ];
  if (last.year !== first.year && flipped(first.gap, last.gap)) {
    out.push(`يتحول ${definite(first.gap)} إلى ${gapKindLabel(last.gap)} بحلول ${last.year} بنحو ${units(last.gap)} وحدة سكنية.`);
  } else {
    if (peak.year !== first.year) {
      const change = pct(Math.abs(peak.gap) - Math.abs(first.gap), first.gap);
      const changeText = comparable(first.gap, peak.gap) ? `، بتغير ${change > 0 ? '+' : ''}${change}% عن ${first.year}` : '';
      out.push(`يصل ${definite(peak.gap)} إلى ذروته عام ${peak.year} عند ${units(peak.gap)} وحدة سكنية${changeText}.`);
    }
    // When the last year is the peak, the bullet above already says the gap widens.
    if (last.year !== first.year && last.year !== peak.year) {
      out.push(`${direction(first.gap, last.gap)} الفجوة بحلول ${last.year} لتبلغ ${units(last.gap)} وحدة سكنية (${gapKindLabel(last.gap)}).`);
    }
  }
  const forecast = a.filter((p) => !p.isActual);
  if (forecast.length) {
    const from = forecast[0].year;
    const range = from === last.year ? `${from}` : `${from}–${last.year}`;
    const assumption = city.vacancyAssumptionPct != null ? ` وفق افتراض معدل شواغر ${city.vacancyAssumptionPct}%` : '';
    out.push(`تُعرض قيم ${range} كتقديرات مستقبلية${assumption}.`);
  }
  return out;
}

function monthlyInsights(city: SupplyDemandGapCity): string[] {
  const out: string[] = [];
  const { monthExpected: exp, monthActual: act } = city.kpis;
  if (exp && act) {
    const diff = Math.abs(act.gap) - Math.abs(exp.gap);
    const lead = `بلغت الفجوة الفعلية في ${city.cityName} ${units(act.gap)} وحدة سكنية (${gapKindLabel(act.gap)}) في ${act.label}`;
    out.push(
      comparable(act.gap, exp.gap)
        ? `${lead}، مقابل ${units(exp.gap)} وحدة متوقعة؛ أي ${diff <= 0 ? 'أقل' : 'أعلى'} من المتوقع بنسبة ${Math.abs(pct(diff, exp.gap))}%.`
        : `${lead}، مقابل ${units(exp.gap)} وحدة متوقعة (${gapKindLabel(exp.gap)}).`,
    );
  }
  const actual = city.monthly.filter((m) => m.isActual);
  if (actual.length >= 2) {
    const a0 = actual[0];
    const aN = actual[actual.length - 1];
    out.push(
      flipped(a0.gap, aN.gap)
        ? `تتحول الفجوة الفعلية من ${gapKindLabel(a0.gap)} ${units(a0.gap)} وحدة في ${a0.label} إلى ${gapKindLabel(aN.gap)} ${units(aN.gap)} وحدة في ${aN.label}.`
        : `${direction(a0.gap, aN.gap)} الفجوة الفعلية من ${units(a0.gap)} وحدة في ${a0.label} إلى ${units(aN.gap)} وحدة في ${aN.label}.`,
    );
  }
  const lastMonth = city.monthly[city.monthly.length - 1];
  if (lastMonth && !lastMonth.isActual) {
    const lastActual = actual[actual.length - 1];
    out.push(
      lastActual && flipped(lastActual.gap, lastMonth.gap)
        ? `يتحول ${definite(lastActual.gap)} إلى ${gapKindLabel(lastMonth.gap)} بنهاية ${lastMonth.label} بنحو ${units(lastMonth.gap)} وحدة سكنية وفق المسار المتوقع.`
        : `تُقدَّر الفجوة بنهاية ${lastMonth.label} بنحو ${units(lastMonth.gap)} وحدة سكنية (${gapKindLabel(lastMonth.gap)}) وفق المسار المتوقع.`,
    );
  }
  return out;
}
