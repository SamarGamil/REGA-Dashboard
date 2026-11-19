import { test } from 'node:test';
import assert from 'node:assert/strict';
import { InputHistory } from '../src/app/utils/input-history.ts';

test('ArrowUp recalls the latest message, then older ones, and stays at the oldest', () => {
  const h = new InputHistory();
  h.push('سؤال أول');
  h.push('سؤال ثاني');

  assert.equal(h.prev(''), 'سؤال ثاني');
  assert.equal(h.prev(''), 'سؤال أول');
  assert.equal(h.prev(''), 'سؤال أول'); // oldest — no wrap-around
});

test('ArrowDown walks forward and restores the unsent draft past the newest', () => {
  const h = new InputHistory();
  h.push('أ');
  h.push('ب');

  assert.equal(h.prev('مسودة لم تُرسل'), 'ب');
  assert.equal(h.prev(''), 'أ');
  assert.equal(h.next(), 'ب');
  assert.equal(h.next(), 'مسودة لم تُرسل'); // draft restored, browsing ends
  assert.equal(h.browsing, false);
  assert.equal(h.next(), null); // not browsing anymore
});

test('typing stops the browsing session', () => {
  const h = new InputHistory();
  h.push('أ');
  h.prev('');
  assert.equal(h.browsing, true);

  h.stopBrowsing();
  assert.equal(h.browsing, false);
  assert.equal(h.next(), null);
  assert.equal(h.prev('نص جديد'), 'أ'); // a fresh session starts with the new draft
  assert.equal(h.next(), 'نص جديد');
});

test('blank entries are ignored and consecutive duplicates collapse', () => {
  const h = new InputHistory();
  h.push('  ');
  assert.equal(h.prev(''), null);

  h.push('سؤال');
  h.push('سؤال');
  assert.equal(h.prev(''), 'سؤال');
  assert.equal(h.prev(''), 'سؤال'); // only stored once
});

test('sending while browsing resets the session and clears the draft', () => {
  const h = new InputHistory();
  h.push('أ');
  h.prev('مسودة');
  h.push('ب');
  assert.equal(h.browsing, false);
  assert.equal(h.prev(''), 'ب');
  assert.equal(h.next(), ''); // draft was cleared by the send
});
