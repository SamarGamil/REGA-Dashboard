import type { RentalIndicatorPoint } from '../models/rental-controls';

export interface TimelinePoint {
  label: string;
  value: number;
  display: string;
}

export const IMPACT_PERIODS: readonly string[] = ['2024-Q4', '2025-Q1', '2025-Q2', '2025-Q3', '2025-Q4', '2026-Q1', '2026-Q2', '2026-Q3'];

export const IMPACT_LABELS: readonly string[] = [
  'الربع الرابع 2024',
  'الربع الأول 2025',
  'الربع الثاني 2025',
  'الربع الثالث 2025',
  'الربع الرابع 2025',
  'الربع الأول 2026',
  'الربع الثاني 2026',
  'الربع الثالث 2026',
];

/** Index (in IMPACT_PERIODS) of the newest quarter the selected period has data for; the last quarter when unknown. */
export function latestImpactIndex(points: readonly RentalIndicatorPoint[]): number {
  const indexes = points.map((p) => IMPACT_PERIODS.indexOf(p.period)).filter((i) => i >= 0);
  return indexes.length ? Math.max(...indexes) : IMPACT_PERIODS.length - 1;
}

// FABRICATED: the intermediate quarters are synthesized from before/previous (previous+3.5, previous+1.8),
// not real data. Replace with a real impact time-series when the backend provides one.
// `afterValue` is the reading of the latest quarter (`latestIndex`); `previousValue` the reading of the quarter before
// it when known (defaults to `afterValue`). Quarters after the selected period have no point.
export function buildImpactTimeline(
  beforeValue: string,
  afterValue: string,
  previousValue = afterValue,
  latestIndex = IMPACT_PERIODS.length - 1,
): (TimelinePoint | null)[] {
  const before = parseFloat(beforeValue);
  const after = parseFloat(afterValue);
  const previous = parseFloat(previousValue);
  if (Number.isNaN(before) || Number.isNaN(after) || Number.isNaN(previous)) return [];

  const valueAt = (i: number): number => {
    if (i === latestIndex) return after;
    if (i < 3) return before;
    return i === 3 ? previous + 3.5 : i === 4 ? previous + 1.8 : previous;
  };

  return IMPACT_LABELS.map((label, i) => {
    if (i > latestIndex) return null;
    const value = Math.round(valueAt(i) * 10) / 10;
    return { label, value, display: `${value}%` };
  });
}

export function buildIncreaseTimeline(points: readonly RentalIndicatorPoint[]): (TimelinePoint | null)[] {
  return IMPACT_PERIODS.map((period, i) => {
    const p = points.find((pt) => pt.period === period);
    return p ? { label: IMPACT_LABELS[i], value: p.value, display: `${p.value}%` } : null;
  });
}
