import { test } from 'node:test';
import assert from 'node:assert/strict';
import { applyRtl } from '../src/app/utils/chart-theme.ts';

test('empty option gains no axes', () => {
  assert.deepEqual(applyRtl({}), {});
});

test('vertical chart: x inverted, y moved right', () => {
  const o: any = applyRtl({ xAxis: { type: 'category' }, yAxis: { type: 'value' }, series: [] });
  assert.equal(o.xAxis[0].inverse, true);
  assert.equal(o.yAxis[0].position, 'right');
});

test('dual y-axes swap sides', () => {
  const o: any = applyRtl({ xAxis: {}, yAxis: [{}, {}], series: [] });
  assert.deepEqual(o.yAxis.map((a: any) => a.position), ['right', 'left']);
});

test('already-inverse axis is toggled back', () => {
  const o: any = applyRtl({ xAxis: { inverse: true }, yAxis: {}, series: [] });
  assert.equal(o.xAxis[0].inverse, false);
});

test('rotated x-axis labels slant the mirrored way', () => {
  const o: any = applyRtl({ xAxis: { axisLabel: { rotate: 30, fontSize: 10 } }, yAxis: {}, series: [] });
  assert.deepEqual(o.xAxis[0].axisLabel, { rotate: -30, fontSize: 10 });
  const flat: any = applyRtl({ xAxis: { axisLabel: { fontSize: 10 } }, yAxis: {}, series: [] });
  assert.deepEqual(flat.xAxis[0].axisLabel, { fontSize: 10 });
});

test('horizontal bar: radius mirrored', () => {
  const o: any = applyRtl({
    xAxis: { type: 'value' },
    yAxis: { type: 'category' },
    series: [{ type: 'bar', itemStyle: { borderRadius: [0, 4, 4, 0] } }],
  });
  assert.deepEqual(o.series[0].itemStyle.borderRadius, [4, 0, 0, 4]);
});

test('per-point radius mirrored, numeric radius untouched', () => {
  const o: any = applyRtl({
    xAxis: {},
    yAxis: {},
    series: [{ type: 'bar', itemStyle: { borderRadius: 0 }, data: [{ value: 1, itemStyle: { borderRadius: [0, 4, 4, 0] } }, 2] }],
  });
  assert.equal(o.series[0].itemStyle.borderRadius, 0);
  assert.deepEqual(o.series[0].data[0].itemStyle.borderRadius, [4, 0, 0, 4]);
  assert.equal(o.series[0].data[1], 2);
});

test('grid left/right swapped', () => {
  const o: any = applyRtl({ xAxis: {}, yAxis: {}, series: [], grid: { top: 10, left: 170, right: 20 } });
  assert.deepEqual(o.grid, { top: 10, left: 20, right: 170 });
});

test('grid with only left set', () => {
  const o: any = applyRtl({ xAxis: {}, yAxis: {}, series: [], grid: { left: 50 } });
  assert.deepEqual(o.grid, { right: 50 });
});

test('legend reversed from series names, aligned right', () => {
  const o: any = applyRtl({
    xAxis: {},
    yAxis: {},
    legend: { bottom: 0 },
    series: [{ type: 'line', name: 'أ' }, { type: 'line', name: 'ب' }],
  });
  assert.deepEqual(o.legend, { bottom: 0, align: 'right', data: ['ب', 'أ'] });
});

test('explicit legend.data reversed; hidden legend untouched', () => {
  const a: any = applyRtl({ xAxis: {}, yAxis: {}, series: [], legend: { data: ['x', 'y'] } });
  assert.deepEqual(a.legend.data, ['y', 'x']);
  const b: any = applyRtl({ xAxis: {}, yAxis: {}, series: [], legend: { show: false } });
  assert.deepEqual(b.legend, { show: false });
});

test('pie: only tooltip class added', () => {
  const pie = { tooltip: { trigger: 'item' }, series: [{ type: 'pie', data: [] }] } as any;
  const o: any = applyRtl(pie);
  assert.equal(o.tooltip.className, 'echarts-tooltip-rtl');
  assert.equal(o.series, pie.series);
  assert.equal(o.xAxis, undefined);
});

test('input is not mutated', () => {
  const input: any = { xAxis: { type: 'category' }, yAxis: {}, series: [] };
  applyRtl(input);
  assert.deepEqual(input, { xAxis: { type: 'category' }, yAxis: {}, series: [] });
});
