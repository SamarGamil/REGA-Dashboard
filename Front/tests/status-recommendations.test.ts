import { test } from 'node:test';
import assert from 'node:assert/strict';
import { statusRecommendations } from '../src/app/utils/status-recommendations.ts';

const city = (name: string, operationalStatus: string) => ({ name, operationalStatus });

test('one row per status, in action order, with the period cities grouped under it', () => {
  const recs = statusRecommendations([
    city('حائل', 'يتطلب دراسة استباقية'),
    city('نجران', 'يتطلب دراسة تفصيلية'),
    city('الرياض', 'الضوابط مفعلة'),
    city('جدة', 'يتطلب دراسة استباقية'),
    city('بريدة', 'لا إجراء'),
  ]);

  assert.deepEqual(recs.map((r) => r.city), [
    'الرياض',
    'نجران',
    'المدن التي تتطلب دراسة استباقية: حائل، جدة',
  ]);
  assert.match(recs[0].text, /^الاستمرار في متابعة أثر الضوابط المفعلة/);
  assert.match(recs[1].text, /^يتطلب دراسة تفصيلية لتقييم أسباب الضغط/);
  assert.match(recs[2].text, /^يتطلب مراقبة دورية للمدن/);
});

test('several cities of one status share a single row', () => {
  const recs = statusRecommendations([
    city('الرياض', 'الضوابط مفعلة'),
    city('جدة', 'الضوابط مفعلة'),
  ]);
  assert.equal(recs.length, 1);
  assert.equal(recs[0].city, 'الرياض، جدة');
});

test('statuses with no city in the period are left out', () => {
  assert.deepEqual(statusRecommendations([city('بريدة', 'لا إجراء')]), []);
  assert.deepEqual(statusRecommendations([]), []);
});
