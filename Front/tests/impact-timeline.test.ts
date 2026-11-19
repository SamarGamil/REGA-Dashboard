import { test } from 'node:test';
import assert from 'node:assert/strict';
import { buildImpactTimeline, buildIncreaseTimeline, latestImpactIndex } from '../src/app/utils/impact-timeline.ts';

const point = (period: string, value: number) => ({ period, value, isLast: false });
const values = (t: ReturnType<typeof buildImpactTimeline>) => t.map((p) => p?.value ?? null);

test('latestImpactIndex follows the newest quarter of the selected period', () => {
  assert.equal(latestImpactIndex([point('2026-Q1', 14), point('2026-Q2', 9)]), 6);
  assert.equal(latestImpactIndex([point('2026-Q2', 9), point('2026-Q3', -1)]), 7);
  assert.equal(latestImpactIndex([]), 7);
});

test('Q3 view: Q2 2026 is the previous reading and Q3 2026 the latest', () => {
  const t = buildImpactTimeline('15.7%', '8.9%', '9.7%', 7);
  assert.deepEqual(values(t), [15.7, 15.7, 15.7, 13.2, 11.5, 9.7, 9.7, 8.9]);
});

test('Q2 view: the line stops at Q2 2026 with its own reading, no Q3 point', () => {
  const t = buildImpactTimeline('15.7%', '9.7%', '9.7%', 6);
  assert.deepEqual(values(t), [15.7, 15.7, 15.7, 13.2, 11.5, 9.7, 9.7, null]);
});

test('rental-increase timeline has no point past the selected quarter', () => {
  const t = buildIncreaseTimeline([point('2025-Q2', 17), point('2026-Q2', 9)]);
  assert.equal(t[2]?.value, 17);
  assert.equal(t[6]?.value, 9);
  assert.equal(t[7], null);
});
