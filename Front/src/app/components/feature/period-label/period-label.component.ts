import { Component, inject } from '@angular/core';
import { PortalPeriodService } from '@/services/portal-period.service';

/** The selected portal period as the page header shows it (read-only; chosen with app-period-filter). */
@Component({
  selector: 'app-period-label',
  standalone: true,
  template: `
    <div class="flex items-center gap-2">
      <i class="ri-calendar-2-line" style="color: #111827; font-size: 17px"></i>
      <span style="font-size: 16px; font-weight: 700; color: #111827">{{ portal.label() }}</span>
    </div>
  `,
})
export class PeriodLabelComponent {
  readonly portal = inject(PortalPeriodService);
}
