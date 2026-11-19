import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NumberBadgeComponent } from '@/components/base/number-badge.component';
import { MarketRecommendation } from '@/models/rental-controls';

@Component({
  selector: 'app-executive-recommendations',
  standalone: true,
  imports: [CommonModule, NumberBadgeComponent],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
      @for (rec of recommendations; track rec.priority) {
        <div
          class="bg-white rounded-xl p-4 flex flex-col"
          style="border: 1px solid #E5EBEF; box-shadow: 0 1px 3px rgba(0,47,86,0.03)"
        >
          <div class="flex items-center gap-2 mb-2">
            <app-number-badge [number]="rec.priority" size="compact" />
            <h5 class="text-sm font-extrabold text-[#0F172A] leading-tight">{{ rec.title }}</h5>
          </div>

          <div class="flex-1">
            <div class="flex items-center gap-1.5 mb-1.5">
              <i class="ri-map-pin-line text-sm text-[#94A3B8]"></i>
              <span class="text-sm text-[#4B5563] font-medium">{{ rec.affectedCities }}</span>
            </div>
            <p class="text-sm text-[#64748B] leading-relaxed mb-3">{{ rec.reason }}</p>
          </div>

          <button
            (click)="handleAction(rec)"
            class="flex items-center gap-1.5 text-sm font-bold cursor-pointer transition-colors whitespace-nowrap mt-auto"
            style="color: #00848D"
          >
            <i [class]="getActionIcon(rec.action) + ' text-sm'"></i>
            {{ rec.cta }}
            <i class="ri-arrow-left-s-line text-sm"></i>
          </button>
        </div>
      }
    </div>
  `,
})
export class ExecutiveRecommendationsComponent {
  @Input({ required: true }) recommendations: MarketRecommendation[] = [];
  @Output() compareRequested = new EventEmitter<void>();

  constructor(private router: Router) {}

  getActionIcon(action: string): string {
    if (action === 'analyze') return 'ri-search-eye-line';
    if (action === 'indicators') return 'ri-bar-chart-line';
    return 'ri-scales-line';
  }

  handleAction(item: MarketRecommendation): void {
    if (item.action === 'indicators') {
      this.router.navigate(['/rental-indicators/indicators']);
    } else if (item.action === 'analyze') {
      this.router.navigate(['/rental-indicators/city-analysis']);
    } else {
      this.compareRequested.emit();
    }
  }
}
