import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PeriodLabelComponent } from '../period-label/period-label.component';

export interface BreadcrumbItem {
  label: string;
  href?: string;
}

/**
 * Global reusable page header used on every dashboard page.
 * RIGHT — dominant page title + description; LEFT — reporting period (the selected portal period on pages that follow it) + last-updated.
 */
@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [CommonModule, PeriodLabelComponent],
  template: `
    <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between py-4 md:py-5 mb-5">
      <div class="flex-1 min-w-0">
        @if (breadcrumbs && breadcrumbs.length > 0) {
          <nav class="flex items-center gap-1.5 text-sm text-[#4B5563] mb-2 flex-wrap">
            @for (item of breadcrumbs; track $index; let index = $index) {
              <span class="flex items-center gap-1.5">
                @if (index > 0) {
                  <span class="text-[#CBD5E1]">/</span>
                }
                @if (item.href) {
                  <a [href]="item.href" class="hover:text-[#00848D] transition-colors cursor-pointer whitespace-nowrap">{{ item.label }}</a>
                } @else {
                  <span class="text-[#0F172A] font-bold whitespace-nowrap">{{ item.label }}</span>
                }
              </span>
            }
          </nav>
        }

        <h1 class="font-extrabold text-[#0F172A] tracking-tight leading-tight text-[22px] md:text-3xl">{{ title }}</h1>

        @if (subtitle) {
          <p class="mt-1.5 leading-snug text-[15px] md:text-[17px]" style="color: #536174; font-weight: 400">{{ subtitle }}</p>
        }
      </div>

      @if (hasMeta) {
        <div class="flex-shrink-0 flex flex-col gap-1.5 md:pl-8">
          @if (portalPeriod) {
            <app-period-label />
          } @else if (reportingPeriod) {
            <div class="flex items-center gap-2">
              <i class="ri-calendar-2-line" style="color: #111827; font-size: 17px"></i>
              <span style="font-size: 16px; font-weight: 700; color: #111827">{{ reportingPeriod }}</span>
            </div>
          }
          @if (lastUpdated) {
            <div class="flex items-center gap-2">
              <span style="font-size: 13px; color: #6B7280">اخر بيانات متاحه: {{ lastUpdated }}</span>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class PageHeaderComponent {
  @Input() title!: string;
  @Input() subtitle?: string;
  @Input() reportingPeriod?: string;
  @Input() lastUpdated?: string;
  @Input() breadcrumbs?: BreadcrumbItem[];
  // Rental-indicators pages whose data follows the selected portal period show that period instead of the static one.
  @Input() portalPeriod = false;

  get hasMeta(): boolean {
    return !!(this.portalPeriod || this.reportingPeriod || this.lastUpdated);
  }
}
