import { Injectable, computed, inject, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, catchError, combineLatest, finalize, of, switchMap, tap } from 'rxjs';
import { ReportsService } from './reports.service';
import { ReportingPeriodService } from './reporting-period.service';
import { ReportPeriods } from '../models/reports';
import { findOption, latestMonthBeyondCurrent } from '../utils/period-options';

/** Period shown on the indicators, city-analysis and city-status pages: shared by the three pages, in memory only. */
@Injectable({ providedIn: 'root' })
export class PortalPeriodService {
  private readonly reports = inject(ReportsService);
  private readonly reportingPeriod = inject(ReportingPeriodService);
  private requested = false;

  readonly periods = signal<ReportPeriods>({ current: '', monthly: [], quarterly: [] });
  /** null = the current quarter (the live data). */
  readonly selected = signal<string | null>(null);
  readonly loading = signal(false);

  readonly selectedId = computed(() => this.selected() ?? this.periods().current);
  readonly selectedOption = computed(() => findOption(this.periods(), this.selectedId()));
  // Before the period list arrives: the live quarter's label (what the header showed before).
  readonly label = computed(() => this.selectedOption()?.label || this.reportingPeriod.current());

  // One watcher for the app's lifetime, shared by every page (a toObservable lives as long as its injector).
  private readonly selected$ = toObservable(this.selected);

  ensureLoaded(): void {
    if (this.requested) return;
    this.requested = true;
    this.reports.getPeriods().subscribe({
      next: (periods) => {
        this.periods.set(periods);
        // The portal opens on the latest available data: a month past the current quarter (e.g. October on an
        // open Q3) becomes the default selection; otherwise the current quarter stays the default.
        const beyond = latestMonthBeyondCurrent(periods);
        if (this.selected() === null && beyond) this.selected.set(beyond);
      },
      error: () => (this.requested = false),
    });
  }

  select(id: string): void {
    this.selected.set(id === this.periods().current ? null : id);
  }

  /**
   * Runs `load` for the current selection and again whenever it changes or `reload$` emits; a newer run cancels the
   * older request. When a load fails the selection reverts to the last period this page loaded (the live data if
   * none), so the label matches the page.
   */
  connect<T>(load: (period?: string) => Observable<T>, reload$: Observable<unknown> = of(null)): Observable<T> {
    this.ensureLoaded();
    let lastLoaded: string | null = null;
    return combineLatest([this.selected$, reload$]).pipe(
      switchMap(([period]) => {
        this.loading.set(true);
        return load(period ?? undefined).pipe(
          tap(() => (lastLoaded = period)),
          catchError(() => {
            if (this.selected() !== lastLoaded) this.selected.set(lastLoaded);
            return EMPTY;
          }),
          finalize(() => this.loading.set(false)),
        );
      }),
    );
  }
}
