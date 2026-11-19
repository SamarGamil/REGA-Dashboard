import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  FORECAST_OPACITY,
  GAP_COLORS,
  buildAnnualGapOption,
  buildMonthlyGapOption,
  formatUnits,
  gapTooltipHtml,
  monthAxisLabel,
} from '../src/app/pages/rental-controls/indicators/components/gap-chart-options.ts';

const annual = [
  { year: 2025, supply: 2301878, demand: 2441532, gap: -139654, expectedGap: -150328, isActual: true },
  { year: 2026, supply: 2340870, demand: 2530424, gap: -189554, expectedGap: -189554, isActual: false },
];

const month = (period: string, label: string, gap: number, isActual: boolean) =>
  ({ period, label, supply: 100, demand: 200, gap, expectedGap: gap, isActual });

const monthly = [
  month('2026-07', 'يوليو 2026', -133152, true),
  month('2026-08', 'أغسطس 2026', -130998, true),
  month('2026-09', 'سبتمبر 2026', -179748, false),
  month('2026-10', 'أكتوبر 2026', -183017, false),
];

test('formatUnits shows absolute grouped values', () => {
  assert.equal(formatUnits(-130998), '130,998');
  assert.equal(formatUnits(150328.4), '150,328');
});

test('monthAxisLabel keeps the month name only', () => {
  assert.equal(monthAxisLabel('أغسطس 2026'), 'أغسطس');
});

test('annual option: actual years solid, forecast years faded with a dashed gap line', () => {
  const o: any = buildAnnualGapOption(annual);
  assert.deepEqual(o.xAxis.data, ['2025', '2026']);
  assert.equal(o.yAxis.length, 2);
  const [supply, demand, actual, forecast] = o.series;
  assert.deepEqual([supply.type, supply.yAxisIndex], ['bar', 0]);
  assert.deepEqual(supply.data.map((d: any) => d.value), [2301878, 2340870]);
  assert.deepEqual(supply.data.map((d: any) => d.itemStyle.opacity), [1, FORECAST_OPACITY]);
  assert.equal(supply.data[0].itemStyle.color, GAP_COLORS.supply);
  assert.deepEqual(demand.data.map((d: any) => d.value), [2441532, 2530424]);
  assert.equal(demand.data[0].itemStyle.color, GAP_COLORS.demand);
  assert.deepEqual([actual.type, actual.yAxisIndex, actual.data], ['line', 1, [139654, null]]);
  assert.deepEqual(forecast.data, [139654, 189554]);
  assert.equal(forecast.lineStyle.type, 'dashed');
  assert.equal(actual.lineStyle.color, GAP_COLORS.gap);
  assert.deepEqual(forecast.markLine.data, [{ xAxis: 1 }]);
});

test('monthly option: forecast bars are faded', () => {
  const o: any = buildMonthlyGapOption(monthly);
  assert.deepEqual(o.xAxis.data, ['يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر']);
  const opacities = o.series[0].data.map((d: any) => d.itemStyle.opacity);
  assert.deepEqual(opacities, [1, 1, FORECAST_OPACITY, FORECAST_OPACITY]);
  assert.deepEqual(o.series[1].data.map((d: any) => d.value), [200, 200, 200, 200]);
});

test('monthly option: solid line over actuals, dashed line from the last actual month on', () => {
  const o: any = buildMonthlyGapOption(monthly);
  const actual = o.series.find((s: any) => s.name === 'الفجوة السكنية');
  const forecast = o.series.find((s: any) => s.name === 'الفجوة السكنية (تقديرات)');
  assert.deepEqual(actual.data, [133152, 130998, null, null]);
  assert.deepEqual(forecast.data, [null, 130998, 179748, 183017]);
  assert.equal(forecast.lineStyle.type, 'dashed');
  assert.equal(actual.lineStyle.type, 'solid');
  assert.deepEqual(forecast.markLine.data, [{ xAxis: 2 }]);
  assert.equal(forecast.markLine.label.formatter, 'بداية التقديرات');
});

test('monthly option: all-actual data has no forecast line or marker', () => {
  const o: any = buildMonthlyGapOption(monthly.slice(0, 2));
  const forecast = o.series.find((s: any) => s.name === 'الفجوة السكنية (تقديرات)');
  assert.deepEqual(forecast.data, [null, null]);
  assert.equal(forecast.markLine, undefined);
});

test('monthly option: all-forecast data is entirely dashed', () => {
  const o: any = buildMonthlyGapOption(monthly.slice(2));
  const actual = o.series.find((s: any) => s.name === 'الفجوة السكنية');
  const forecast = o.series.find((s: any) => s.name === 'الفجوة السكنية (تقديرات)');
  assert.deepEqual(actual.data, [null, null]);
  assert.deepEqual(forecast.data, [179748, 183017]);
  assert.equal(forecast.markLine, undefined);
});

test('tooltip for an actual point: actual supply and gap, expected demand, and the expected gap', () => {
  const html = gapTooltipHtml('2025', annual[0]);
  for (const s of ['2025', 'بيانات فعلية', 'العرض السكني (فعلي)', '2,301,878', 'الطلب السكني (متوقع)', '2,441,532',
    'الفجوة السكنية (فعلية)', '139,654', 'الفجوة السكنية (متوقعة)', '150,328', 'عجز']) {
    assert.ok(html.includes(s), `missing ${s}`);
  }
  assert.ok(!html.includes('تقديرات'));
});

test('tooltip for a forecast point: expected values only, gap listed once', () => {
  const html = gapTooltipHtml('2026', annual[1]);
  for (const s of ['2026', 'تقديرات', 'العرض السكني (متوقع)', '2,340,870', 'الطلب السكني (متوقع)', 'الفجوة السكنية (متوقعة)', '189,554']) {
    assert.ok(html.includes(s), `missing ${s}`);
  }
  assert.ok(!html.includes('فعلي'));
  assert.equal(html.split('189,554').length - 1, 1);
});

test('both charts use the custom tooltip with the full period title', () => {
  const a: any = buildAnnualGapOption(annual);
  assert.equal(a.tooltip.formatter([{ dataIndex: 0 }]), gapTooltipHtml('2025', annual[0]));
  const m: any = buildMonthlyGapOption(monthly);
  assert.equal(m.tooltip.formatter([{ dataIndex: 1 }]), gapTooltipHtml('أغسطس 2026', monthly[1]));
});
