import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  findOption, latestMonthBeyondCurrent, latestQuarterOf, monthChoices, openQuarter, openYear, parsePeriod, periodYears,
  quarterChoices,
} from '../src/app/utils/period-options.ts';

// As GET /reports/periods returns them today: newest first; July 2026 has no data of its own but is listed.
const periods = {
  current: '2026-Q3',
  monthly: [
    { id: '2026-09', label: 'سبتمبر 2026' },
    { id: '2026-08', label: 'أغسطس 2026' },
    { id: '2026-07', label: 'يوليو 2026' },
    { id: '2026-06', label: 'يونيو 2026' },
  ],
  quarterly: [{ id: '2026-Q3', label: 'الربع الثالث 2026' }, { id: '2026-Q2', label: 'الربع الثاني 2026' }],
};

test('parses a period into year, quarter and month', () => {
  assert.deepEqual(parsePeriod('2026-07'), { year: 2026, quarter: 3, month: 7 });
  assert.deepEqual(parsePeriod('2026-Q2'), { year: 2026, quarter: 2, month: null });
  assert.equal(parsePeriod(''), null);
});

test('finds a period option by id', () => {
  assert.equal(findOption(periods, '2026-08')?.label, 'أغسطس 2026');
  assert.equal(findOption(periods, '2026-Q2')?.label, 'الربع الثاني 2026');
  assert.equal(findOption(periods, '2019-Q1'), undefined);
});

test('years come from the periods that exist', () => {
  assert.deepEqual(periodYears(periods), [2026]);
});

test('only quarters with data are selectable', () => {
  assert.deepEqual(
    quarterChoices(periods, 2026).map((q) => [q.value, q.label, q.enabled]),
    [['2026-Q1', 'الربع الأول', false], ['2026-Q2', 'الربع الثاني', true], ['2026-Q3', 'الربع الثالث', true], ['2026-Q4', 'الربع الرابع', false]],
  );
});

test("a quarter lists its three months; only months with data are selectable", () => {
  assert.deepEqual(
    monthChoices(periods, 2026, 3).map((m) => [m.value, m.label, m.enabled]),
    [['2026-07', 'يوليو', true], ['2026-08', 'أغسطس', true], ['2026-09', 'سبتمبر', true]],
  );
  assert.deepEqual(monthChoices(periods, 2026, 2).map((m) => m.enabled), [false, false, true]);
});

test("choosing a year opens its newest quarter", () => {
  assert.equal(latestQuarterOf(periods, 2026), '2026-Q3');
  assert.equal(latestQuarterOf(periods, 2025), undefined);
});
// A dataset dated after its newest quarter (e.g. August data whose newest quarter is still Q2) lists the month but not
// the month's quarter; at a year boundary a year can have months but no quarter.
const lagging = {
  current: '2026-Q2',
  monthly: [{ id: '2026-08', label: 'أغسطس 2026' }, { id: '2026-07', label: 'يوليو 2026' }, { id: '2026-06', label: 'يونيو 2026' }],
  quarterly: [{ id: '2026-Q2', label: 'الربع الثاني 2026' }],
};
const newYear = {
  current: '2026-Q4',
  monthly: [{ id: '2027-01', label: 'يناير 2027' }, { id: '2026-12', label: 'ديسمبر 2026' }],
  quarterly: [{ id: '2026-Q4', label: 'الربع الرابع 2026' }],
};

test('a quarter with months but no quarterly period is selectable and opens its newest month', () => {
  assert.deepEqual(quarterChoices(lagging, 2026).map((q) => q.enabled), [false, true, true, false]);
  assert.equal(openQuarter(lagging, '2026-Q3'), '2026-08');
  assert.equal(openQuarter(lagging, '2026-Q2'), '2026-Q2');
  assert.equal(openQuarter(lagging, '2026-Q4'), undefined);
});

test('a year opens its newest quarter, else its newest month', () => {
  assert.equal(openYear(periods, 2026), '2026-Q3');
  assert.equal(openYear(newYear, 2027), '2027-01');
  assert.equal(openYear(newYear, 2026), '2026-Q4');
  assert.equal(openYear(newYear, 2025), undefined);
});

test('the portal default advances to a month past the current quarter, else stays on the quarter', () => {
  // October data on an open Q3: the newest month lies beyond the current quarter.
  const october = {
    current: '2026-Q3',
    monthly: [{ id: '2026-10', label: 'أكتوبر 2026' }, ...periods.monthly],
    quarterly: periods.quarterly,
  };
  assert.equal(latestMonthBeyondCurrent(october), '2026-10');
  // The newest month inside the current quarter: no advance.
  assert.equal(latestMonthBeyondCurrent(periods), null);
  // Across a year boundary (January 2027 on an open Q4 2026).
  assert.equal(latestMonthBeyondCurrent(newYear), '2027-01');
  assert.equal(latestMonthBeyondCurrent({ current: '2026-Q3', monthly: [], quarterly: periods.quarterly }), null);
});
