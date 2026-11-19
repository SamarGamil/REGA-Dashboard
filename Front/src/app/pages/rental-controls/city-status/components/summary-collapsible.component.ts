import { Component, ElementRef, Input, QueryList, ViewChildren, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import type { NationalSummaryData, CityData, OperationalStage } from '@/models/rental-controls';

const classificationLegend = [
  { key: "sustainable", label: "نمو مستدام", range: "≤6%", colorClass: "bg-emerald-100/70" },
  { key: "elevated", label: "نمو مرتفع", range: "7%-9%", colorClass: "bg-amber-100/70" },
  { key: "accelerated", label: "نمو متسارع", range: "≥10%", colorClass: "bg-rose-100/70" },
];

const growthPathStyleMap: Record<string, { bg: string; border: string; text: string }> = {
  'نمو مستدام': {
    bg: 'linear-gradient(135deg, #F0F8F7 0%, #E8F6F3 100%)',
    border: '#75CEC820',
    text: '#005257',
  },
  'نمو مرتفع': {
    bg: 'linear-gradient(135deg, #FDF8F0 0%, #FDF5E6 100%)',
    border: '#D4A85320',
    text: '#8B6914',
  },
  'نمو متسارع': {
    bg: 'linear-gradient(135deg, #FFF5F3 0%, #FDEDEC 100%)',
    border: '#E7A5A020',
    text: '#C4534B',
  },
};

interface TooltipAdjust { flipUp: boolean; shiftLeft: number; }

@Component({
  selector: 'app-summary-collapsible',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './summary-collapsible.component.html',
})
export class SummaryCollapsibleComponent {
  @Input({ required: true }) summaryData!: NationalSummaryData;
  @Input() operationalStages: OperationalStage[] = [];
  @Input() cities: CityData[] = [];

  @ViewChildren('tooltip') tooltips!: QueryList<ElementRef<HTMLDivElement>>;

  expanded = signal(true);
  hoveredGrowthPath = signal<string | null>(null);
  tooltipAdjust = signal<TooltipAdjust>({ flipUp: false, shiftLeft: 0 });

  growthPaths = ['نمو مستدام', 'نمو مرتفع', 'نمو متسارع'];

  classificationLegend = classificationLegend;

  styleFor(path: string) { return growthPathStyleMap[path]; }

  countFor(path: string): number {
    switch (path) {
      case 'نمو مستدام': return this.summaryData.sustainableCities;
      case 'نمو مرتفع': return this.summaryData.elevatedCities;
      case 'نمو متسارع': return this.summaryData.acceleratedCities;
      default: return 0;
    }
  }

  opsCount(label: string): number {
    return this.operationalStages.find(s => s.label === label)?.count ?? 0;
  }

  citiesForPath(path: string): string[] {
    return this.cities.filter(c => c.classification === path).map(c => c.name);
  }

  get totalCities(): number {
    return this.summaryData.acceleratedCities + this.summaryData.elevatedCities + this.summaryData.sustainableCities;
  }

  onEnter(path: string): void {
    this.hoveredGrowthPath.set(path);
    requestAnimationFrame(() => this.recalcTooltip());
  }

  onLeave(): void {
    this.hoveredGrowthPath.set(null);
    this.tooltipAdjust.set({ flipUp: false, shiftLeft: 0 });
  }

  private recalcTooltip(): void {
    const el = this.tooltips?.first?.nativeElement;
    if (!el) return;
    const rect = el.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;
    let flipUp = false;
    let shiftLeft = 0;

    if (rect.bottom > vh - 8) { flipUp = true; }
    if (rect.right > vw - 8) { shiftLeft = rect.right - vw + 8; }
    if (rect.left < 8) { shiftLeft = -(8 - rect.left); }

    const prev = this.tooltipAdjust();
    if (prev.flipUp === flipUp && prev.shiftLeft === shiftLeft) return;
    this.tooltipAdjust.set({ flipUp, shiftLeft });
  }

  tooltipStyle(): Record<string, string> {
    const adj = this.tooltipAdjust();
    const style: Record<string, string> = {
      backgroundColor: '#002F56',
      color: '#FFFFFF',
      boxShadow: '0 4px 16px rgba(0,47,86,0.2)',
    };
    if (adj.flipUp) {
      style['bottom'] = '100%';
      style['top'] = 'auto';
    } else {
      style['top'] = '100%';
      style['bottom'] = 'auto';
    }
    if (adj.shiftLeft !== 0) {
      style['transform'] = `translateX(${-adj.shiftLeft}px)`;
    }
    return style;
  }
}
