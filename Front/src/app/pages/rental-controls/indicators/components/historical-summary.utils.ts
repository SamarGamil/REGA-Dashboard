import { HistoricalCityRow } from '@/models/rental-controls';

// Values are compared at one decimal so cities that display the same number count as tied.
const key = (v: number): number => Math.round(v * 10) / 10;

/** All cities sharing the highest (or lowest) value in `year`; cities without a value are skipped. */
export function extremeCities(cities: HistoricalCityRow[], year: string, kind: 'max' | 'min'): HistoricalCityRow[] {
  const withValue = cities.filter((c) => c.values[year] != null);
  if (withValue.length === 0) return [];
  const values = withValue.map((c) => key(c.values[year] as number));
  const target = kind === 'max' ? Math.max(...values) : Math.min(...values);
  return withValue.filter((_, i) => values[i] === target);
}

/** Change over the years each city actually has data (missing years are not treated as 0). */
export function cityChange(city: HistoricalCityRow, years: string[]): number | null {
  const present = years.filter((y) => city.values[y] != null);
  if (present.length < 2) return null;
  return (city.values[present[present.length - 1]] as number) - (city.values[present[0]] as number);
}

/** All cities sharing the largest absolute change. */
export function largestChangeCities(cities: HistoricalCityRow[], years: string[]): HistoricalCityRow[] {
  const changes = cities.map((c) => cityChange(c, years));
  const abs = changes.map((ch) => (ch == null ? -1 : key(Math.abs(ch))));
  const best = Math.max(...abs);
  if (best <= 0) return [];
  return cities.filter((_, i) => abs[i] === best);
}

/** First year in which any city has a value. */
export function firstYearWithData(cities: HistoricalCityRow[], years: string[]): string | undefined {
  return years.find((y) => cities.some((c) => c.values[y] != null));
}

/** "أ" / "أ وب" / "أ، ب وج" */
export function joinCityNames(names: string[]): string {
  if (names.length <= 1) return names[0] ?? '';
  return `${names.slice(0, -1).join('، ')} و${names[names.length - 1]}`;
}
