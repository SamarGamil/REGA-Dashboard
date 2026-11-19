import { Component, Input } from '@angular/core';

/**
 * Global reusable numbered sequence badge.
 * Light teal circle with dark teal bold number.
 */
@Component({
  selector: 'app-number-badge',
  standalone: true,
  template: `
    <div
      [class]="(size === 'compact' ? 'w-9 h-9' : 'w-10 h-10') + ' rounded-full flex items-center justify-center flex-shrink-0'"
      [style.backgroundColor]="'#E5F5F3'"
    >
      <span
        [class]="(size === 'compact' ? 'text-[15px]' : 'text-[17px]') + ' font-bold leading-none'"
        [style.color]="'#005B5F'"
      >{{ number }}</span>
    </div>
  `,
})
export class NumberBadgeComponent {
  @Input() number!: number | string;
  @Input() size: 'default' | 'compact' = 'default';
}
