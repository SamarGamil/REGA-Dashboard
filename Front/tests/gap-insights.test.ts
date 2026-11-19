import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  buildGapInsights,
  gapKindLabel,
  gapRangeLabel,
} from '../src/app/pages/rental-controls/indicators/components/gap-insights.utils.ts';
import * as gapInsights from '../src/app/pages/rental-controls/indicators/components/gap-insights.utils.ts';

test('gap view defaults to monthly', () => {
  assert.equal(gapInsights.DEFAULT_GAP_MODE, 'monthly');
});

const riyadh: any = {
  cityId: 'riyadh',
  cityName: 'الرياض',
  vacancyAssumptionPct: 10,
  lastActualPeriod: '2026-08',
  kpis: {
    firstYear: { year: 2025, gap: -150328, actualGap: -139654 },
    secondYear: { year: 2026, gap: -189554, actualGap: null },
    monthExpected: { period: '2026-08', label: 'أغسطس 2026', gap: -176479 },
    monthActual: { period: '2026-08', label: 'أغسطس 2026', gap: -130998 },
  },
  annual: [
    { year: 2025, supply: 2301878, demand: 2441532, gap: -139654, expectedGap: -150328, isActual: true },
    { year: 2026, supply: 2340870, demand: 2530424, gap: -189554, expectedGap: -189554, isActual: false },
    { year: 2027, supply: 2390000, demand: 2560000, gap: -170000, expectedGap: -170000, isActual: false },
  ],
  monthly: [
    { period: '2026-01', label: 'يناير 2026', supply: 2308512, demand: 2448940, gap: -140428, isActual: true },
    { period: '2026-08', label: 'أغسطس 2026', supply: 2369796, demand: 2500794, gap: -130998, isActual: true },
    { period: '2026-12', label: 'ديسمبر 2026', supply: 2340870, demand: 2530424, gap: -189554, isActual: false },
  ],
};

test('gap kind labels', () => {
  assert.equal(gapKindLabel(-1), 'عجز');
  assert.equal(gapKindLabel(5), 'فائض');
  assert.equal(gapKindLabel(0), 'توازن');
});

test('range labels per mode', () => {
  assert.equal(gapRangeLabel(riyadh, 'annual'), '2025–2027');
  assert.equal(gapRangeLabel(riyadh, 'monthly'), 'يناير – ديسمبر 2026');
});

test('annual insights: actual first year vs expected, peak, direction, forecast years', () => {
  assert.deepEqual(buildGapInsights(riyadh, 'annual'), [
    'بلغ العجز الفعلي في الرياض 139,654 وحدة سكنية عام 2025 (المتوقع 150,328)، مقابل طلب متوقع 2,441,532 وحدة وعرض فعلي 2,301,878 وحدة.',
    'يصل العجز إلى ذروته عام 2026 عند 189,554 وحدة سكنية، بتغير +35.7% عن 2025.',
    'تتسع الفجوة بحلول 2027 لتبلغ 170,000 وحدة سكنية (عجز).',
    'تُعرض قيم 2026–2027 كتقديرات مستقبلية وفق افتراض معدل شواغر 10%.',
  ]);
});

test('annual insights: two years only — no repeated widening bullet', () => {
  const twoYears: any = { ...riyadh, annual: riyadh.annual.slice(0, 2) };
  assert.deepEqual(buildGapInsights(twoYears, 'annual'), [
    'بلغ العجز الفعلي في الرياض 139,654 وحدة سكنية عام 2025 (المتوقع 150,328)، مقابل طلب متوقع 2,441,532 وحدة وعرض فعلي 2,301,878 وحدة.',
    'يصل العجز إلى ذروته عام 2026 عند 189,554 وحدة سكنية، بتغير +35.7% عن 2025.',
    'تُعرض قيم 2026 كتقديرات مستقبلية وفق افتراض معدل شواغر 10%.',
  ]);
});

test('annual insights: a surplus turning into a shortage is described, not given a percentage', () => {
  const makkah: any = {
    ...riyadh,
    cityName: 'مكة المكرمة',
    kpis: { ...riyadh.kpis, firstYear: { year: 2025, gap: 12210, actualGap: 220 } },
    annual: [
      { year: 2025, supply: 802214, demand: 801994, gap: 220, expectedGap: 12210, isActual: true },
      { year: 2026, supply: 820000, demand: 831817, gap: -11817, expectedGap: -11817, isActual: false },
    ],
  };
  assert.deepEqual(buildGapInsights(makkah, 'annual'), [
    'بلغ الفائض الفعلي في مكة المكرمة 220 وحدة سكنية عام 2025 (المتوقع 12,210)، مقابل طلب متوقع 801,994 وحدة وعرض فعلي 802,214 وحدة.',
    'يتحول الفائض إلى عجز بحلول 2026 بنحو 11,817 وحدة سكنية.',
    'تُعرض قيم 2026 كتقديرات مستقبلية وفق افتراض معدل شواغر 10%.',
  ]);
});

test('monthly insights: actual and expected on opposite sides get no percentage', () => {
  const flipped: any = {
    ...riyadh,
    kpis: { ...riyadh.kpis, monthExpected: { period: '2026-08', label: 'أغسطس 2026', gap: 3000 } },
  };
  assert.equal(
    buildGapInsights(flipped, 'monthly')[0],
    'بلغت الفجوة الفعلية في الرياض 130,998 وحدة سكنية (عجز) في أغسطس 2026، مقابل 3,000 وحدة متوقعة (فائض).',
  );
});

test('monthly insights: actual vs expected, actual trend, year-end forecast', () => {
  assert.deepEqual(buildGapInsights(riyadh, 'monthly'), [
    'بلغت الفجوة الفعلية في الرياض 130,998 وحدة سكنية (عجز) في أغسطس 2026، مقابل 176,479 وحدة متوقعة؛ أي أقل من المتوقع بنسبة 25.8%.',
    'تضيق الفجوة الفعلية من 140,428 وحدة في يناير 2026 إلى 130,998 وحدة في أغسطس 2026.',
    'تُقدَّر الفجوة بنهاية ديسمبر 2026 بنحو 189,554 وحدة سكنية (عجز) وفق المسار المتوقع.',
  ]);
});

const monthPt = (period: string, label: string, gap: number, isActual: boolean) =>
  ({ period, label, supply: 0, demand: 0, gap, expectedGap: gap, isActual });

test('monthly insights: a forecast that turns a surplus into a shortage says so (Dammam pattern)', () => {
  const dammam: any = {
    ...riyadh,
    cityName: 'الدمام',
    kpis: { ...riyadh.kpis, monthExpected: { period: '2026-08', label: 'أغسطس 2026', gap: 4242 }, monthActual: { period: '2026-08', label: 'أغسطس 2026', gap: 15199 } },
    monthly: [monthPt('2026-01', 'يناير 2026', 14000, true), monthPt('2026-08', 'أغسطس 2026', 15199, true), monthPt('2026-12', 'ديسمبر 2026', -316, false)],
  };
  assert.deepEqual(buildGapInsights(dammam, 'monthly'), [
    'بلغت الفجوة الفعلية في الدمام 15,199 وحدة سكنية (فائض) في أغسطس 2026، مقابل 4,242 وحدة متوقعة؛ أي أعلى من المتوقع بنسبة 258.3%.',
    'تتسع الفجوة الفعلية من 14,000 وحدة في يناير 2026 إلى 15,199 وحدة في أغسطس 2026.',
    'يتحول الفائض إلى عجز بنهاية ديسمبر 2026 بنحو 316 وحدة سكنية وفق المسار المتوقع.',
  ]);
});

test('monthly insights: actual months that change side are described, not narrowed', () => {
  const flip: any = {
    ...riyadh,
    monthly: [monthPt('2026-01', 'يناير 2026', 500, true), monthPt('2026-08', 'أغسطس 2026', -300, true)],
  };
  assert.equal(
    buildGapInsights(flip, 'monthly')[1],
    'تتحول الفجوة الفعلية من فائض 500 وحدة في يناير 2026 إلى عجز 300 وحدة في أغسطس 2026.',
  );
});

test('a zero base never yields a 0% comparison', () => {
  const zeroExp: any = { ...riyadh, kpis: { ...riyadh.kpis, monthExpected: { period: '2026-08', label: 'أغسطس 2026', gap: 0 } } };
  assert.equal(
    buildGapInsights(zeroExp, 'monthly')[0],
    'بلغت الفجوة الفعلية في الرياض 130,998 وحدة سكنية (عجز) في أغسطس 2026، مقابل 0 وحدة متوقعة (توازن).',
  );
  const zeroFirst: any = {
    ...riyadh,
    annual: [
      { year: 2025, supply: 10, demand: 10, gap: 0, expectedGap: 0, isActual: false },
      { year: 2026, supply: 10, demand: 30, gap: -20, expectedGap: -20, isActual: false },
    ],
  };
  assert.equal(buildGapInsights(zeroFirst, 'annual')[1], 'يصل العجز إلى ذروته عام 2026 عند 20 وحدة سكنية.');
});

test('surplus city and missing assumption', () => {
  const surplus: any = {
    ...riyadh,
    cityName: 'الدمام',
    vacancyAssumptionPct: null,
    annual: [{ year: 2025, supply: 900, demand: 800, gap: 100, expectedGap: 100, isActual: false }],
  };
  assert.deepEqual(buildGapInsights(surplus, 'annual'), [
    'يبلغ الفائض المتوقع في الدمام 100 وحدة سكنية عام 2025، مقابل طلب متوقع 800 وحدة وعرض متوقع 900 وحدة.',
    'تُعرض قيم 2025 كتقديرات مستقبلية.',
  ]);
});

test('no data gives no bullets', () => {
  const empty: any = { ...riyadh, annual: [], monthly: [], kpis: { firstYear: null, secondYear: null, monthExpected: null, monthActual: null } };
  assert.deepEqual(buildGapInsights(empty, 'annual'), []);
  assert.deepEqual(buildGapInsights(empty, 'monthly'), []);
  assert.equal(gapRangeLabel(empty, 'annual'), '');
  assert.equal(gapRangeLabel(empty, 'monthly'), '');
});
