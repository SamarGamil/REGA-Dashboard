import type { EChartsOption } from 'echarts';
import type { GapAnnualPoint, GapMonthlyPoint } from '@/models/rental-controls';

// Type-only imports: this file is unit-tested with `node --test`.

export const GAP_COLORS = { supply: '#5B8DB8', demand: '#E08A3C', gap: '#8E6BBF' } as const;
export const FORECAST_OPACITY = 0.45;

const SUPPLY = 'العرض السكني';
const DEMAND = 'الطلب السكني';
const GAP = 'الفجوة السكنية';
const GAP_FORECAST = 'الفجوة السكنية (تقديرات)';

export function formatUnits(value: number): string {
  return Math.round(Math.abs(value)).toLocaleString('en-US');
}

export function monthAxisLabel(label: string): string {
  return label.split(' ')[0];
}

const axisLabel = { fontSize: 12, color: '#64748B' };

function baseOption(categories: string[]): EChartsOption {
  return {
    grid: { left: 16, right: 16, top: 36, bottom: 8, containLabel: true },
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#fff',
      borderColor: '#E5EBEF',
      borderWidth: 1,
      textStyle: { color: '#0F2137' },
    },
    legend: { show: false },
    xAxis: {
      type: 'category',
      data: categories,
      axisTick: { show: false },
      axisLine: { lineStyle: { color: '#E5EBEF' } },
      axisLabel,
    },
    yAxis: [
      {
        type: 'value',
        name: 'وحدة سكنية',
        nameTextStyle: { color: '#64748B', fontSize: 11 },
        axisLabel: { ...axisLabel, formatter: (v: number) => formatUnits(v) },
        splitLine: { lineStyle: { type: 'dashed', color: '#EEF2F6' } },
      },
      {
        type: 'value',
        name: 'الفجوة',
        nameTextStyle: { color: '#64748B', fontSize: 11 },
        axisLabel: { ...axisLabel, formatter: (v: number) => formatUnits(v) },
        splitLine: { show: false },
      },
    ],
  };
}

export function buildAnnualGapOption(points: GapAnnualPoint[]): EChartsOption {
  const years = points.map((p) => String(p.year));
  return buildActualVsForecastOption(years, years, points);
}

export function buildMonthlyGapOption(points: GapMonthlyPoint[]): EChartsOption {
  return buildActualVsForecastOption(points.map((p) => monthAxisLabel(p.label)), points.map((p) => p.label), points);
}

interface GapPoint { supply: number; demand: number; gap: number; expectedGap: number; isActual: boolean; }

const kindLabel = (gap: number) => (gap < 0 ? 'عجز' : gap > 0 ? 'فائض' : 'توازن');

// One row per measure, each labelled actual or expected, so the shared boundary point of the two gap lines and the
// empty half of the split line never show up as duplicate or "—" rows.
export function gapTooltipHtml(title: string, p: GapPoint): string {
  const row = (color: string, label: string, value: string) =>
    `<div style="display:flex;align-items:center;justify-content:space-between;gap:16px;margin-top:4px">` +
    `<span style="display:flex;align-items:center;gap:6px;color:#4B5563">` +
    `<span style="width:8px;height:8px;border-radius:9999px;background:${color}"></span>${label}</span>` +
    `<span style="font-weight:700;color:#0F2137">${value}</span></div>`;
  const gapValue = (g: number) => `${formatUnits(g)} <span style="font-weight:400;color:#64748B">${kindLabel(g)}</span>`;

  const rows = [
    row(GAP_COLORS.supply, `${SUPPLY} (${p.isActual ? 'فعلي' : 'متوقع'})`, formatUnits(p.supply)),
    row(GAP_COLORS.demand, `${DEMAND} (متوقع)`, formatUnits(p.demand)),
    row(GAP_COLORS.gap, `${GAP} (${p.isActual ? 'فعلية' : 'متوقعة'})`, gapValue(p.gap)),
  ];
  if (p.isActual) rows.push(row('#C4B5E0', `${GAP} (متوقعة)`, gapValue(p.expectedGap)));

  return (
    `<div dir="rtl" style="text-align:right;min-width:220px">` +
    `<div style="font-weight:700;color:#002F56">${title} ` +
    `<span style="font-weight:400;color:#64748B">— ${p.isActual ? 'بيانات فعلية' : 'تقديرات'}</span></div>` +
    rows.join('') +
    `</div>`
  );
}

// Actual points: solid bars + solid gap line; forecast points: faded bars + dashed gap line from the last actual on.
function buildActualVsForecastOption(categories: string[], titles: string[], points: GapPoint[]): EChartsOption {
  const firstForecast = points.findIndex((p) => !p.isActual);
  const hasForecast = firstForecast !== -1;
  const lastActual = hasForecast ? firstForecast - 1 : points.length - 1;
  const gap = points.map((p) => Math.abs(p.gap));

  const bars = (name: string, color: string, value: (p: GapPoint) => number) => ({
    type: 'bar' as const,
    name,
    yAxisIndex: 0,
    barMaxWidth: 18,
    data: points.map((p) => ({
      value: value(p),
      itemStyle: { color, opacity: p.isActual ? 1 : FORECAST_OPACITY, borderRadius: [4, 4, 0, 0] },
    })),
  });

  const line = (name: string, type: 'solid' | 'dashed', data: (number | null)[]) => ({
    type: 'line' as const,
    name,
    yAxisIndex: 1,
    symbol: 'circle',
    symbolSize: 6,
    connectNulls: false,
    itemStyle: { color: GAP_COLORS.gap },
    lineStyle: { color: GAP_COLORS.gap, width: 2, type },
    data,
  });

  const forecast = line(
    GAP_FORECAST,
    'dashed',
    gap.map((v, i) => (hasForecast && i >= Math.max(lastActual, 0) ? v : null)),
  );
  const forecastWithMarker =
    hasForecast && firstForecast > 0
      ? {
          ...forecast,
          markLine: {
            symbol: 'none',
            silent: true,
            lineStyle: { type: 'dashed' as const, color: '#94A3B8' },
            label: { formatter: 'بداية التقديرات', color: '#64748B', fontSize: 11 },
            // Index, not the category name: month names repeat when a series spans more than 12 months.
            data: [{ xAxis: firstForecast }],
          },
        }
      : forecast;

  const base = baseOption(categories);
  return {
    ...base,
    tooltip: {
      ...(base.tooltip as object),
      formatter: (params: unknown) => {
        const first = (Array.isArray(params) ? params[0] : params) as { dataIndex?: number } | undefined;
        const i = first?.dataIndex;
        return i == null || !points[i] ? '' : gapTooltipHtml(titles[i], points[i]);
      },
    },
    series: [
      bars(SUPPLY, GAP_COLORS.supply, (p) => p.supply),
      bars(DEMAND, GAP_COLORS.demand, (p) => p.demand),
      line(GAP, 'solid', gap.map((v, i) => (i <= lastActual ? v : null))),
      forecastWithMarker,
    ],
  };
}
