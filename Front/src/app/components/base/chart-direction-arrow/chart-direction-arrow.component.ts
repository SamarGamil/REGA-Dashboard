import { Component } from '@angular/core';

// Charts on the rental pages run right → left (RTL, oldest period on the right); this arrow sits under a chart's
// x-axis, at its left end, to show which way time moves.
@Component({
  selector: 'app-chart-direction-arrow',
  standalone: true,
  host: { class: 'block' },
  template: `
    <div class="flex justify-start" dir="ltr">
      <i
        class="ri-arrow-left-line text-[18px] text-[#64748B]"
        role="img"
        aria-label="اتجاه الزمن من اليمين إلى اليسار"
        title="اتجاه الزمن من اليمين إلى اليسار"
      ></i>
    </div>
  `,
})
export class ChartDirectionArrowComponent {}
