import { Component, Input } from '@angular/core';
import { RentalIndicatorPoint } from '@/models/rental-controls';
import { ImpactLineChartComponent, ImpactSeries } from './impact-line-chart.component';
import {
  IMPACT_LABELS, TimelinePoint, buildImpactTimeline, buildIncreaseTimeline, latestImpactIndex,
} from '@/utils/impact-timeline';

// FABRICATED: authored insight bullets, not derived from backend data. Replace when a real source exists.
const staticInsights = (drop: string) => [
  { icon: 'ri-checkbox-circle-line', color: '#00848D', text: `تراجع النمو الإيجاري المركب بمقدار ${drop} مقارنة بفترة ما قبل التفعيل` },
  { icon: 'ri-checkbox-circle-line', color: '#00848D', text: 'بدأت مؤشرات العبء السكني في الاستقرار' },
  { icon: 'ri-checkbox-circle-line', color: '#00848D', text: 'تباطأت الزيادة الإيجارية في العقود القائمة' },
  { icon: 'ri-time-line', color: '#D4A853', text: 'لا تزال فترة القياس غير كافية لتقييم الأثر النهائي' },
];

const GROWTH_NAME = 'معدل النمو الإيجاري المركب';
const GROWTH_COLOR = '#00848D';
const INCREASE_NAME = 'الزيادة الإيجارية';
const INCREASE_COLOR = '#C9A227';

function formatPointChange(delta: number): string {
  if (delta < 0) return `${Math.abs(delta)}- نقاط مئوية`;
  if (delta > 0) return `${delta}+ نقاط مئوية`;
  return '0 نقاط مئوية';
}

@Component({
  selector: 'app-impact-panel',
  standalone: true,
  imports: [ImpactLineChartComponent],
  templateUrl: './impact-panel.component.html',
})
export class ImpactPanelComponent {
  @Input() cityName = '';
  @Input() available = false;
  @Input() beforeValue = '';
  /** Compound growth of the selected period's quarter. */
  @Input() afterValue = '';
  /** Compound growth of the quarter before the selected one, when known. */
  @Input() previousValue = '';
  @Input() increasePoints: RentalIndicatorPoint[] = [];

  readonly ACTIVATION_INDEX = 3;
  readonly IMPACT_LABELS = IMPACT_LABELS;
  readonly GROWTH_COLOR = GROWTH_COLOR;
  readonly GROWTH_NAME = GROWTH_NAME;
  readonly INCREASE_COLOR = INCREASE_COLOR;
  readonly INCREASE_NAME = INCREASE_NAME;

  /** Index of the selected period's quarter on the chart's axis; later quarters have no points. */
  get latestIndex(): number {
    return latestImpactIndex(this.increasePoints);
  }

  get timeline(): (TimelinePoint | null)[] {
    return buildImpactTimeline(this.beforeValue, this.afterValue, this.previousValue || this.afterValue, this.latestIndex);
  }

  private get growthDelta(): number {
    return Math.round((parseFloat(this.afterValue) - parseFloat(this.beforeValue)) * 10) / 10;
  }

  /** Change in compound growth since before the activation, e.g. "6.8- نقاط مئوية". */
  get change(): string {
    return Number.isNaN(this.growthDelta) ? '' : formatPointChange(this.growthDelta);
  }

  /** Size of the drop, e.g. "6.8 نقطة مئوية" / "6 نقاط مئوية". */
  get growthDrop(): string {
    const drop = Math.abs(this.growthDelta);
    if (Number.isNaN(drop)) return '';
    return `${drop} ${Number.isInteger(drop) && drop >= 3 && drop <= 10 ? 'نقاط' : 'نقطة'} مئوية`;
  }

  get description(): string {
    return this.growthDelta < 0 ? `انخفض معدل النمو الإيجاري المركب بمقدار ${this.growthDrop} منذ تطبيق الضوابط` : '';
  }

  get hasTimeline(): boolean {
    return this.timeline.filter((p) => p !== null).length >= 4;
  }

  get increaseTimeline(): (TimelinePoint | null)[] {
    return buildIncreaseTimeline(this.increasePoints);
  }

  get hasIncrease(): boolean {
    const t = this.increaseTimeline;
    return t[this.ACTIVATION_INDEX - 1] != null && t[this.latestIndex] != null;
  }

  get increaseBefore(): number {
    return this.increaseTimeline[this.ACTIVATION_INDEX - 1]?.value ?? 0;
  }

  get increaseAfter(): number {
    return this.increaseTimeline[this.latestIndex]?.value ?? 0;
  }

  get increaseDelta(): number {
    return Math.round((this.increaseAfter - this.increaseBefore) * 10) / 10;
  }

  get increaseChange(): string {
    return formatPointChange(this.increaseDelta);
  }

  get descriptionText(): string {
    return this.hasIncrease && this.increaseDelta < 0
      ? `منذ تفعيل الضوابط، انخفض معدل النمو الإيجاري المركب بمقدار ${this.growthDrop}، مع تباطؤ الزيادة الإيجارية خلال الفترة نفسها.`
      : this.description;
  }

  get chartSeries(): ImpactSeries[] {
    const series: ImpactSeries[] = [{ name: GROWTH_NAME, color: GROWTH_COLOR, points: this.timeline }];
    if (this.hasIncrease) series.push({ name: INCREASE_NAME, color: INCREASE_COLOR, points: this.increaseTimeline });
    return series;
  }

  get insights(): { icon: string; color: string; text: string }[] {
    if (!this.hasIncrease) return staticInsights(this.growthDrop);
    const d = this.increaseDelta;
    const items = [
      { icon: 'ri-checkbox-circle-line', color: GROWTH_COLOR, text: `انخفض معدل النمو الإيجاري المركب بمقدار ${this.growthDrop} بعد تفعيل الضوابط` },
      d < 0
        ? { icon: 'ri-checkbox-circle-line', color: INCREASE_COLOR, text: `لوحظ تباطؤ الزيادة الإيجارية بمقدار ${Math.abs(d)} نقاط مئوية خلال الفترة التالية للتفعيل` }
        : { icon: 'ri-error-warning-line', color: INCREASE_COLOR, text: `ارتفعت الزيادة الإيجارية بمقدار ${d} نقاط مئوية خلال الفترة التالية للتفعيل` },
    ];
    if (d < 0) {
      items.push({ icon: 'ri-arrow-left-right-line', color: GROWTH_COLOR, text: 'كلا المؤشرين يتحركان في الاتجاه نفسه نحو الانخفاض بعد تفعيل الضوابط' });
    }
    items.push({ icon: 'ri-time-line', color: '#D4A853', text: 'لا تزال فترة القياس غير كافية لتقييم الأثر النهائي' });
    return items;
  }
}
