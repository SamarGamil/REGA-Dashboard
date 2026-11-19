const enNumberFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

export function formatNumber(n: number): string {
  return enNumberFormat.format(n);
}

export function formatCurrencyShort(value: number, full = false): string {
  if (full) return `${enNumberFormat.format(value)} ر.س`;
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)}B ر.س`;
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(2)}M ر.س`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K ر.س`;
  return `${enNumberFormat.format(value)} ر.س`;
}

export function formatCurrencyLong(value: number): string {
  if (value >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(2)} مليار`;
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(2)} مليون`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)} ألف`;
  return enNumberFormat.format(value);
}

// Compact magnitude label (B/M/K) without a currency suffix, for chart axes/tooltips.
// Keeps both small (sub-million) and very large (billion+) values legible.
export function formatCompact(value: number): string {
  const v = Math.abs(value);
  const sign = value < 0 ? '-' : '';
  if (v >= 1_000_000_000) return `${sign}${(v / 1_000_000_000).toFixed(1)}B`;
  if (v >= 1_000_000) return `${sign}${(v / 1_000_000).toFixed(1)}M`;
  if (v >= 1_000) return `${sign}${(v / 1_000).toFixed(0)}K`;
  return `${sign}${Math.round(v)}`;
}

export function formatPercent(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return `${value}%`;
}

const countFormat = new Intl.NumberFormat('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 1 });

// Transaction counts are share-weighted sums: whole values render plainly (3), fractional ones keep one decimal (27.1).
export function formatCount(n: number | null | undefined): string {
  return countFormat.format(n ?? 0);
}
