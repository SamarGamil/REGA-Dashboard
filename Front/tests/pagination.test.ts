import { test } from 'node:test';
import assert from 'node:assert/strict';
import { pageSlice, pageWindow } from '../src/app/utils/pagination.ts';

test('slices a long list into pages', () => {
  assert.deepEqual(pageSlice(124, 1, 10), { page: 1, totalPages: 13, start: 0, end: 10 });
  assert.deepEqual(pageSlice(124, 13, 10), { page: 13, totalPages: 13, start: 120, end: 124 });
});

test('clamps the page to the available range', () => {
  assert.equal(pageSlice(124, 99, 10).page, 13);
  assert.equal(pageSlice(124, 0, 10).page, 1);
  assert.equal(pageSlice(124, -3, 10).page, 1);
});

test('an empty or short list has a single page', () => {
  assert.deepEqual(pageSlice(0, 3, 10), { page: 1, totalPages: 1, start: 0, end: 0 });
  assert.deepEqual(pageSlice(4, 1, 10), { page: 1, totalPages: 1, start: 0, end: 4 });
});

test('a larger page size shrinks the page count', () => {
  assert.equal(pageSlice(124, 1, 25).totalPages, 5);
  assert.equal(pageSlice(124, 1, 50).totalPages, 3);
});

test('page window shows first, last and neighbours with gaps', () => {
  assert.deepEqual(pageWindow(6, 13), [1, 'gap', 5, 6, 7, 'gap', 13]);
  assert.deepEqual(pageWindow(1, 13), [1, 2, 'gap', 13]);
  assert.deepEqual(pageWindow(13, 13), [1, 'gap', 12, 13]);
});

test('a single skipped page is shown instead of a gap', () => {
  assert.deepEqual(pageWindow(3, 13), [1, 2, 3, 4, 'gap', 13]);
  assert.deepEqual(pageWindow(4, 5), [1, 2, 3, 4, 5]);
});

test('one page needs no window', () => {
  assert.deepEqual(pageWindow(1, 1), [1]);
  assert.deepEqual(pageWindow(1, 0), [1]);
});
