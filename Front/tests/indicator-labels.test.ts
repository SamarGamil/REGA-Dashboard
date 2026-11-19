import { test } from 'node:test';
import assert from 'node:assert/strict';
import { frequencyBadge, symbolicReference, withMonitoringLabel } from '../src/app/utils/indicator-labels.ts';

// \u202A..\u202C set the sign + figure as a left-to-right run (the report's sign placement).
test('adds للمراقبة and shows the reference as a sign, like the exported reports', () => {
  assert.equal(withMonitoringLabel('الحد المرجعي: أكبر من 5%'), 'الحد المرجعي للمراقبة: \u202A> 5%\u202C');
  assert.equal(withMonitoringLabel('الحد المرجعي: أقل من 6%'), 'الحد المرجعي للمراقبة: \u202A< 6%\u202C');
});

test('is idempotent', () => {
  const once = withMonitoringLabel('الحد المرجعي: أقل من 6%');
  assert.equal(withMonitoringLabel(once), once);
});

test('leaves other text untouched', () => {
  assert.equal(withMonitoringLabel('أحدث شهر متاح أغسطس 2026'), 'أحدث شهر متاح أغسطس 2026');
  assert.equal(withMonitoringLabel(''), '');
});

test('symbolic reference mirrors the report mapping', () => {
  assert.equal(symbolicReference('أكبر من 30%'), '\u202A> 30%\u202C');
  assert.equal(symbolicReference('أكثر من 30%'), '\u202A> 30%\u202C');
  assert.equal(symbolicReference('أقل من 5%'), '\u202A< 5%\u202C');
  assert.equal(symbolicReference('9.5% فأكثر'), '\u202A≥ 9.5%\u202C');
  assert.equal(symbolicReference('30% أو أقل'), '\u202A≤ 30%\u202C');
});

test('symbolic reference keeps ranges and dashes', () => {
  assert.equal(symbolicReference('6.5% – 9.4%'), '6.5% – 9.4%');
  assert.equal(symbolicReference('—'), '—');
});

test('frequency badge labels and colours', () => {
  assert.deepEqual(frequencyBadge('monthly'), { label: 'شهري', bg: '#E8F7F6', text: '#00848D' });
  assert.deepEqual(frequencyBadge('quarterly'), { label: 'ربعي', bg: '#EEF2F6', text: '#475569' });
  assert.equal(frequencyBadge(''), null);
});
