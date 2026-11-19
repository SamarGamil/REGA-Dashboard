import type { EChartsOption } from 'echarts';

export const CHART_COLORS = {
  primary: '#00848D',
  accent: '#E8A317',
  positive: '#52C86F',
  grid: '#F1F5F9',
  axis: '#64748B',
  tooltipBg: '#0F172A',
} as const;

export function baseChartTheme(): Pick<EChartsOption, 'grid' | 'tooltip' | 'textStyle'> {
  return {
    textStyle: { fontFamily: 'Tajawal', color: CHART_COLORS.axis },
    grid: { top: 30, right: 20, bottom: 30, left: 50 },
    tooltip: {
      trigger: 'axis',
      backgroundColor: CHART_COLORS.tooltipBg,
      borderWidth: 0,
      textStyle: { color: '#fff', fontFamily: 'Tajawal' },
    },
  };
}

type Obj = Record<string, any>;

const toList = (v: unknown): Obj[] => (v == null ? [] : ((Array.isArray(v) ? v : [v]) as Obj[]));

// borderRadius is [top-left, top-right, bottom-right, bottom-left] in screen space.
function mirrorRadius(style: Obj): Obj {
  const r = style['borderRadius'];
  return Array.isArray(r) && r.length === 4 ? { ...style, borderRadius: [r[1], r[0], r[3], r[2]] } : style;
}

function mirrorBar(series: Obj): Obj {
  if (series['type'] !== 'bar') return series;
  const data = series['data'];
  return {
    ...series,
    ...(series['itemStyle'] && { itemStyle: mirrorRadius(series['itemStyle']) }),
    ...(Array.isArray(data) && {
      data: data.map((d: any) => (d?.itemStyle ? { ...d, itemStyle: mirrorRadius(d.itemStyle) } : d)),
    }),
  };
}

/** Mirrors a chart horizontally for the Arabic (RTL) layout: first category on the right, value axis on the right. */
export function applyRtl(option: EChartsOption): EChartsOption {
  const o = option as Obj;
  const out: Obj = { ...o };

  if (o['tooltip'] && !Array.isArray(o['tooltip'])) {
    out['tooltip'] = { ...o['tooltip'], className: 'echarts-tooltip-rtl' };
  }
  if (o['xAxis'] == null) return out as EChartsOption;

  const series = toList(o['series']);
  out['xAxis'] = toList(o['xAxis']).map(a => {
    const label = a['axisLabel'];
    return {
      ...a,
      inverse: !a['inverse'],
      ...(label?.rotate && { axisLabel: { ...label, rotate: -label.rotate } }),
    };
  });
  // ECharts places an unpositioned first y-axis on the left and the second on the right.
  out['yAxis'] = toList(o['yAxis']).map((a, i) => {
    const side = a['position'] ?? (i === 0 ? 'left' : 'right');
    return { ...a, position: side === 'left' ? 'right' : 'left' };
  });
  out['series'] = series.map(mirrorBar);

  const grid = o['grid'];
  if (grid && !Array.isArray(grid)) {
    const { left, right, ...rest } = grid as Obj;
    out['grid'] = { ...rest, ...(right != null && { left: right }), ...(left != null && { right: left }) };
  }

  const legend = o['legend'];
  if (legend && !Array.isArray(legend) && legend.show !== false) {
    const names: string[] = legend.data ?? series.map(s => s['name']).filter(Boolean);
    out['legend'] = { ...legend, align: 'right', ...(names.length && { data: [...names].reverse() }) };
  }

  return out as EChartsOption;
}
