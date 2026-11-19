import { Component, EventEmitter, Input, OnChanges, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CityIndicatorValue, IndicatorPanelData } from '@/models/rental-controls';
import { IndicatorTooltipComponent } from './indicator-tooltip.component';
import { frequencyBadge, withMonitoringLabel } from '@/utils/indicator-labels';

const LOWER_IS_WORSE = ['vacancy', 'investment-return'];

@Component({
  selector: 'app-indicator-panel',
  standalone: true,
  imports: [CommonModule, IndicatorTooltipComponent],
  templateUrl: './indicator-panel.component.html',
})
export class IndicatorPanelComponent implements OnChanges {
  @Input({ required: true }) data!: IndicatorPanelData;
  @Input() isActive = false;
  @Output() indicatorClick = new EventEmitter<string>();

  readonly hoveredCity = signal<string | null>(null);

  sortedCities: CityIndicatorValue[] = [];

  ngOnChanges(): void {
    this.sortedCities = this.sortCitiesByStatusAndValue(this.data.cities, this.data.id);
  }

  // Lower values are the pressure side (below the reference = دون الحد).
  get higherIsBetter(): boolean {
    return ['vacancy', 'investment-return', 'public-vacancy'].includes(this.data.id);
  }

  get orderingLabel(): string {
    return LOWER_IS_WORSE.includes(this.data.id)
      ? 'الترتيب: حسب الحالة، ثم الأكثر حاجة للمتابعة أولاً (الأقل قيمة)'
      : 'الترتيب: حسب الحالة، ثم الأكثر حاجة للمتابعة أولاً (الأعلى قيمة)';
  }

  get thresholdSummary(): string {
    return withMonitoringLabel(this.data.thresholdSummary);
  }

  get badge(): { label: string; bg: string; text: string } | null {
    return frequencyBadge(this.data.frequency);
  }

  getStatusStyles(status: string): { dot: string; text: string; bg: string } {
    if (status === 'ضمن الحد') {
      return { dot: '#059669', text: '#059669', bg: '#ECFDF5' };
    }
    if (status === 'متابعة') {
      return { dot: '#D97706', text: '#D97706', bg: '#FFFBEB' };
    }
    if (status === '—') {
      return { dot: '#9CA3AF', text: '#6B7280', bg: '#F9FAFB' };
    }
    // متجاوز, دون الحد, تجاوز مرتفع, or any other negative status
    return { dot: '#DC2626', text: '#DC2626', bg: '#FEF2F2' };
  }

  private getStatusPriority(status: string): number {
    if (status === '—') return 0;
    if (status === 'ضمن الحد') return 1;
    if (status === 'متابعة') return 2;
    // Anything else (متجاوز, دون الحد, etc.) = red = highest priority
    return 3;
  }

  private sortCitiesByStatusAndValue(
    cities: CityIndicatorValue[],
    indicatorId: string,
  ): CityIndicatorValue[] {
    const lowerIsWorse = LOWER_IS_WORSE.includes(indicatorId);

    return [...cities].sort((a, b) => {
      const priorityA = this.getStatusPriority(a.status);
      const priorityB = this.getStatusPriority(b.status);

      // Different statuses: red (3) > orange (2) > green (1) > no data (0)
      if (priorityA !== priorityB) {
        return priorityB - priorityA;
      }

      // Same status: sort by value, worst first
      if (lowerIsWorse) {
        // Lower value = worse (more critical)
        return a.value - b.value;
      }
      // Higher value = worse (more critical)
      return b.value - a.value;
    });
  }

  handlePanelClick(): void {
    this.indicatorClick.emit(this.data.id);
  }
}
