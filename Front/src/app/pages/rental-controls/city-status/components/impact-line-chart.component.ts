import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ChartDirectionArrowComponent } from '@/components/base/chart-direction-arrow/chart-direction-arrow.component';
import { TimelinePoint } from '@/utils/impact-timeline';

// Points are aligned to IMPACT_PERIODS; null = no value for that quarter.
export interface ImpactSeries {
  name: string;
  color: string;
  points: (TimelinePoint | null)[];
}

@Component({
  selector: 'app-impact-line-chart',
  standalone: true,
  imports: [CommonModule, ChartDirectionArrowComponent],
  templateUrl: './impact-line-chart.component.html',
})
export class ImpactLineChartComponent {
  @Input() labels: readonly string[] = [];
  @Input() series: ImpactSeries[] = [];
  @Input() activationIndex = 0;

  hoverIndex = signal<number | null>(null);

  readonly W = 600;
  readonly H = 220;
  readonly padT = 4;
  readonly padB = 16;

  private get values(): number[] {
    return this.series.flatMap((s) => s.points.filter((p): p is TimelinePoint => p !== null).map((p) => p.value));
  }

  private get min(): number {
    return Math.floor(Math.min(...this.values)) - 1;
  }

  private get max(): number {
    return Math.ceil(Math.max(...this.values)) + 1;
  }

  private get range(): number {
    return this.max - this.min || 1;
  }

  private get n(): number {
    return this.labels.length;
  }

  private get step(): number {
    return (this.W - this.padT) / (this.n - 1);
  }

  // RTL: index 0 (oldest) is on the right
  x(i: number): number {
    return this.W - this.padT - i * this.step;
  }

  y(v: number): number {
    return this.padT + ((this.max - v) / this.range) * (this.H - this.padT - this.padB);
  }

  get actX(): number {
    return (this.x(this.activationIndex - 1) + this.x(this.activationIndex)) / 2;
  }

  pathFor(s: ImpactSeries): string {
    const parts: string[] = [];
    let penDown = false;
    s.points.forEach((p, i) => {
      if (p === null) {
        penDown = false;
        return;
      }
      parts.push(`${penDown ? 'L' : 'M'} ${this.x(i)} ${this.y(p.value)}`);
      penDown = true;
    });
    return parts.join(' ');
  }

  get hoverRows(): { name: string; color: string; display: string }[] {
    const idx = this.hoverIndex();
    if (idx === null) return [];
    return this.series
      .filter((s) => s.points[idx] != null)
      .map((s) => ({ name: s.name, color: s.color, display: s.points[idx]!.display }));
  }

  get tooltipY(): number {
    const idx = this.hoverIndex();
    if (idx === null) return 0;
    const ys = this.series
      .map((s) => s.points[idx])
      .filter((p): p is TimelinePoint => p != null)
      .map((p) => this.y(p.value));
    return ys.length ? Math.min(...ys) : 0;
  }
}
