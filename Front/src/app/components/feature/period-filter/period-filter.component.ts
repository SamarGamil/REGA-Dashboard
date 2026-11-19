import { Component, Input, booleanAttribute, computed, inject } from '@angular/core';
import { PortalPeriodService } from '@/services/portal-period.service';
import { monthChoices, openQuarter, openYear, parsePeriod, periodYears, quarterChoices } from '@/utils/period-options';

const ALL = 'all';

/**
 * Year / quarter / month dropdowns choosing the portal period (shared by the indicators, city-analysis and
 * city-status pages). "كل الأرباع" (a whole year) stays disabled until yearly data exists; "كل الأشهر" = the quarter.
 * A quarter or year with months but no quarterly period opens its newest month.
 */
@Component({
  selector: 'app-period-filter',
  standalone: true,
  template: `
    <div [class]="labeled ? cardClass : groupClass" dir="rtl" role="group" aria-label="الفترة" [attr.aria-busy]="portal.loading()">
      <!-- Year -->
      <div class="flex items-center" [class.gap-2]="labeled" [class.px-3]="labeled">
        @if (labeled) {
          <span class="text-xs text-[#94A3B8] whitespace-nowrap">السنة</span>
        }
        <div class="relative">
          <select aria-label="السنة" [disabled]="disabled()" (change)="onYear($any($event.target))" [class]="selectClass">
            @for (y of years(); track y) {
              <option [value]="y" [selected]="y === parts()?.year">{{ y }}</option>
            }
          </select>
          <span [class]="chevronClass"><i class="ri-arrow-down-s-line text-[#4B5563] text-sm"></i></span>
        </div>
      </div>

      <div class="w-px h-6 bg-[#E7ECF2] shrink-0"></div>

      <!-- Quarter -->
      <div class="flex items-center" [class.gap-2]="labeled" [class.px-3]="labeled">
        @if (labeled) {
          <span class="text-xs text-[#94A3B8] whitespace-nowrap">الربع</span>
        }
        <div class="relative">
          <select aria-label="الربع" [disabled]="disabled()" (change)="onQuarter($any($event.target))" [class]="selectClass">
            <option [value]="all" disabled>كل الأرباع</option>
            @for (q of quarters(); track q.value) {
              <option [value]="q.value" [disabled]="!q.enabled" [selected]="q.value === selectedQuarter()">{{ q.label }}</option>
            }
          </select>
          <span [class]="chevronClass"><i class="ri-arrow-down-s-line text-[#4B5563] text-sm"></i></span>
        </div>
      </div>

      <div class="w-px h-6 bg-[#E7ECF2] shrink-0"></div>

      <!-- Month -->
      <div class="flex items-center" [class.gap-2]="labeled" [class.px-3]="labeled">
        @if (labeled) {
          <span class="text-xs text-[#94A3B8] whitespace-nowrap">الشهر</span>
        }
        <div class="relative">
          <select aria-label="الشهر" [disabled]="disabled()" (change)="onMonth($any($event.target).value)" [class]="selectClass">
            <option [value]="all" [disabled]="!quarterHasPeriod()" [selected]="!parts()?.month">كل الأشهر</option>
            @for (m of months(); track m.value) {
              <option [value]="m.value" [disabled]="!m.enabled" [selected]="m.value === portal.selectedId()">{{ m.label }}</option>
            }
          </select>
          <span [class]="chevronClass"><i class="ri-arrow-down-s-line text-[#4B5563] text-sm"></i></span>
        </div>
      </div>

      @if (portal.loading()) {
        <span role="status" class="px-2 inline-flex">
          <i class="ri-loader-4-line animate-spin text-[#00848D]" aria-hidden="true"></i>
          <span class="sr-only">جارٍ تحميل بيانات الفترة</span>
        </span>
      }
    </div>
  `,
})
export class PeriodFilterComponent {
  /** A labelled card (indicators, city-analysis); without it, a compact group for a filter row (city-status). */
  @Input({ transform: booleanAttribute }) labeled = false;

  readonly portal = inject(PortalPeriodService);
  readonly all = ALL;

  readonly groupClass = 'flex items-center rounded-xl border border-[#E7ECF2] bg-white overflow-hidden';
  readonly cardClass = 'inline-flex flex-wrap items-center rounded-2xl border border-[#E7ECF2] bg-white px-2 py-1';
  readonly selectClass = 'appearance-none bg-white px-4 py-2.5 pr-9 text-sm font-bold text-[#0F172A] cursor-pointer border-0 focus:outline-none disabled:opacity-60 disabled:cursor-default';
  readonly chevronClass = 'absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 flex items-center justify-center pointer-events-none';

  readonly parts = computed(() => parsePeriod(this.portal.selectedId()));
  readonly years = computed(() => periodYears(this.portal.periods()));
  readonly quarters = computed(() => {
    const p = this.parts();
    return p ? quarterChoices(this.portal.periods(), p.year) : [];
  });
  readonly months = computed(() => {
    const p = this.parts();
    return p ? monthChoices(this.portal.periods(), p.year, p.quarter) : [];
  });
  readonly selectedQuarter = computed(() => {
    const p = this.parts();
    return p ? `${p.year}-Q${p.quarter}` : '';
  });
  // "كل الأشهر" is the quarter itself, so it needs a quarterly period (a quarter can have months only).
  readonly quarterHasPeriod = computed(() => this.portal.periods().quarterly.some((p) => p.id === this.selectedQuarter()));
  // Disabled only until the period list arrives. Not while a period loads: disabling the focused select would drop
  // keyboard focus (arrow keys on a closed select fire change), and a newer choice cancels the older load anyway.
  readonly disabled = computed(() => !this.parts());

  constructor() {
    this.portal.ensureLoaded();
  }

  onYear(select: HTMLSelectElement): void {
    this.open(openYear(this.portal.periods(), +select.value), select, String(this.parts()?.year ?? ''));
  }

  onQuarter(select: HTMLSelectElement): void {
    this.open(select.value === ALL ? undefined : openQuarter(this.portal.periods(), select.value), select, this.selectedQuarter());
  }

  onMonth(value: string): void {
    this.portal.select(value === ALL ? this.selectedQuarter() : value);
  }

  // Selects the period a choice opens; when it opens none, puts the dropdown back so it never shows a choice the page
  // is not on (its [selected] bindings did not change, so Angular would not reset it).
  private open(id: string | undefined, select: HTMLSelectElement, current: string): void {
    if (id) this.portal.select(id);
    else select.value = current;
  }
}
