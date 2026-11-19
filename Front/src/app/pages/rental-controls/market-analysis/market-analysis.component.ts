import { Component, inject, signal, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LayoutComponent } from '@/components/feature/layout/layout.component';
import { RentalControlsService } from '@/services/rental-controls.service';
import { MarketAnalysisData, CityMarketMetric } from '@/models/rental-controls';
import { IndicatorExplorerComponent } from './components/indicator-explorer.component';
import { RelationshipAnalysisComponent } from './components/relationship-analysis.component';
import { ExecutiveRecommendationsComponent } from './components/executive-recommendations.component';

@Component({
  selector: 'app-market-analysis',
  standalone: true,
  imports: [CommonModule, LayoutComponent, IndicatorExplorerComponent, RelationshipAnalysisComponent, ExecutiveRecommendationsComponent],
  templateUrl: './market-analysis.component.html',
})
export class MarketAnalysisComponent {
  private svc = inject(RentalControlsService);
  data = signal<MarketAnalysisData | null>(null);

  @ViewChild('relationshipSection') relationshipSection?: ElementRef<HTMLElement>;

  constructor() {
    this.svc.getMarketAnalysis().subscribe((d) => this.data.set(d));
  }

  scrollToRelationship(): void {
    this.relationshipSection?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // "أبرز الفروقات" — highest city per metric, derived from the real matrix (null-safe).
  insights(): { text: string }[] {
    const d = this.data();
    if (!d) return [];
    const top = (key: keyof CityMarketMetric, label: string) => {
      const rows = d.cities.filter((c) => c[key] != null);
      if (!rows.length) return null;
      const best = rows.reduce((a, b) => ((b[key] as number) > (a[key] as number) ? b : a));
      return { text: `${best.name} تسجل أعلى ${label} بين المدن.` };
    };
    return [
      top('rentalTransactionCount', 'عدد صفقات إيجارية'),
      top('avgSalesTransactionValue', 'متوسط قيمة صفقة بيعية'),
      top('villaRentIndex', 'رقم قياسي لإيجار الفيلا'),
      top('rentToIncome', 'نسبة إيجار إلى الدخل'),
    ].filter((x): x is { text: string } => x !== null);
  }
}
