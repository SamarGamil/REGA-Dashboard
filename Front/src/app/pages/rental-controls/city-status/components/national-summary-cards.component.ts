import { Component, Input, signal } from '@angular/core';
import type { NationalSummaryData, CityData } from '@/models/rental-controls';

const growthPathStyleMap: Record<string, { bg: string; text: string; desc: string }> = {
  'نمو مستدام': {
    bg: '#F0F8F7',
    text: '#005257',
    desc: 'معدل النمو السنوي المركب ≤ 6%',
  },
  'نمو مرتفع': {
    bg: '#FDF5E6',
    text: '#8B6914',
    desc: 'معدل النمو السنوي المركب 7%-9%',
  },
  'نمو متسارع': {
    bg: '#FDEDEC',
    text: '#C4534B',
    desc: 'معدل النمو السنوي المركب ≥ 10%',
  },
};

@Component({
  selector: 'app-national-summary-cards',
  standalone: true,
  templateUrl: './national-summary-cards.component.html',
})
export class NationalSummaryCardsComponent {
  @Input({ required: true }) data!: NationalSummaryData;
  @Input() cities: CityData[] = [];

  hoveredGrowthPath = signal<string | null>(null);
  growthPaths = ['نمو مستدام', 'نمو مرتفع', 'نمو متسارع'];

  styleFor(path: string) { return growthPathStyleMap[path]; }

  countFor(path: string): number {
    switch (path) {
      case 'نمو مستدام': return this.data.sustainableCities;
      case 'نمو مرتفع': return this.data.elevatedCities;
      case 'نمو متسارع': return this.data.acceleratedCities;
      default: return 0;
    }
  }

  citiesFor(path: string): string[] {
    return this.cities.filter(c => c.classification === path).map(c => c.name);
  }
}
