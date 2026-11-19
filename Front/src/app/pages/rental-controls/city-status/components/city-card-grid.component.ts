import { Component, Input } from '@angular/core';
import type { CityData } from '@/models/rental-controls';
import { CityCardComponent } from './city-card.component';

@Component({
  selector: 'app-city-card-grid',
  standalone: true,
  imports: [CityCardComponent],
  template: `
    <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-5" dir="rtl">
      @for (city of cities; track city.id) {
        <app-city-card [city]="city" [periodLabel]="periodLabel" />
      }
    </div>
  `,
})
export class CityCardGridComponent {
  @Input() cities: CityData[] = [];
  /** The period the cards' figures belong to, passed through to each card. */
  @Input() periodLabel = '';
}
