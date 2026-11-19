import type { ReportPeriodOption, ReportPeriods } from '../models/reports';

export const QUARTER_NAMES = ['الربع الأول', 'الربع الثاني', 'الربع الثالث', 'الربع الرابع'];
export const MONTH_NAMES = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'];

export interface PeriodParts {
  year: number;
  quarter: number;
  month: number | null;
}

/** A dropdown entry: value is a period id (yyyy-Qn / yyyy-MM); disabled when no data exists for it. */
export interface PeriodChoice {
  value: string;
  label: string;
  enabled: boolean;
}

/** "2026-07" → 2026 / Q3 / month 7; "2026-Q2" → 2026 / Q2 / no month; null for anything else. */
export function parsePeriod(id: string): PeriodParts | null {
  const month = /^(\d{4})-(\d{2})$/.exec(id);
  if (month) return { year: +month[1], quarter: Math.ceil(+month[2] / 3), month: +month[2] };
  const quarter = /^(\d{4})-Q([1-4])$/.exec(id);
  return quarter ? { year: +quarter[1], quarter: +quarter[2], month: null } : null;
}

export function findOption(periods: ReportPeriods, id: string): ReportPeriodOption | undefined {
  return [...periods.monthly, ...periods.quarterly].find((p) => p.id === id);
}

/** Years that have data, newest first. */
export function periodYears(periods: ReportPeriods): number[] {
  const years = [...periods.monthly, ...periods.quarterly].map((p) => parsePeriod(p.id)?.year).filter((y): y is number => !!y);
  return [...new Set(years)].sort((a, b) => b - a);
}

/** The year's four quarters; a quarter is selectable when it has a quarterly period or any month with data. */
export function quarterChoices(periods: ReportPeriods, year: number): PeriodChoice[] {
  const ids = new Set([...periods.quarterly.map((p) => p.id), ...periods.monthly.map((p) => quarterOf(p.id))]);
  return QUARTER_NAMES.map((label, i) => {
    const value = `${year}-Q${i + 1}`;
    return { value, label, enabled: ids.has(value) };
  });
}

/** The quarter's three months; a month is selectable when its monthly period exists. */
export function monthChoices(periods: ReportPeriods, year: number, quarter: number): PeriodChoice[] {
  const ids = new Set(periods.monthly.map((p) => p.id));
  return [1, 2, 3].map((k) => {
    const month = (quarter - 1) * 3 + k;
    const value = `${year}-${String(month).padStart(2, '0')}`;
    return { value, label: MONTH_NAMES[month - 1], enabled: ids.has(value) };
  });
}

/** The newest quarter of a year that has data. */
export function latestQuarterOf(periods: ReportPeriods, year: number): string | undefined {
  return newest(periods.quarterly.map((p) => p.id).filter((id) => id.startsWith(`${year}-Q`)));
}

/** What choosing a quarter opens: the quarter itself when it has a quarterly period, else its newest month. */
export function openQuarter(periods: ReportPeriods, quarterId: string): string | undefined {
  if (periods.quarterly.some((p) => p.id === quarterId)) return quarterId;
  return newest(periods.monthly.map((p) => p.id).filter((id) => quarterOf(id) === quarterId));
}

/** What choosing a year opens: its newest quarter, else (a year with months only) its newest month. */
export function openYear(periods: ReportPeriods, year: number): string | undefined {
  return latestQuarterOf(periods, year) ?? newest(periods.monthly.map((p) => p.id).filter((id) => id.startsWith(`${year}-`)));
}

/**
 * The newest month lying after the current quarter (a monthly update moved the data past it, e.g. October on an
 * open Q3), else null: the portal opens on it so the picker shows the latest available data.
 */
export function latestMonthBeyondCurrent(periods: ReportPeriods): string | null {
  const current = parsePeriod(periods.current);
  const newestMonth = newest(periods.monthly.map((p) => p.id));
  const m = newestMonth ? parsePeriod(newestMonth) : null;
  if (!current || !m || !newestMonth) return null;
  return m.year * 4 + m.quarter > current.year * 4 + current.quarter ? newestMonth : null;
}

function quarterOf(id: string): string | undefined {
  const p = parsePeriod(id);
  return p ? `${p.year}-Q${p.quarter}` : undefined;
}

// Ids of one kind (yyyy-MM or yyyy-Qn) sort chronologically as strings.
function newest(ids: string[]): string | undefined {
  return [...ids].sort().at(-1);
}
