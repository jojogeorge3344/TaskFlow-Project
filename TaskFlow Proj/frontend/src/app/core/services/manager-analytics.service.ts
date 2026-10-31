import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, of, switchMap } from 'rxjs';
import { defaultFilter, localToday, toQueryParams, validateRange } from '../analytics/analytics-filter';
import { AuthService } from '../auth/auth.service';
import { AnalyticsFilterOptions, AnalyticsFilterState, TeamAnalytics, TeamFilterOptions } from '../models';
import { FetchResult } from './manager-analytics.model';

/** State for the Manager analytics page. The server limits everything to the manager's own team, and the filter options
 * only ever list that team's employees and projects; the department and manager filters do not exist here. */
@Injectable({ providedIn: 'root' })
export class ManagerAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = '/api/v1/dashboard/manager/analytics';
  private readonly refresh$ = new Subject<void>();
  private started = false;
  private serverDates = false;
  private session = 0;

  readonly filter = signal<AnalyticsFilterState>(defaultFilter(localToday()));
  readonly options = signal<AnalyticsFilterOptions | null>(null);
  readonly data = signal<TeamAnalytics | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  private readonly serverToday = signal<string | null>(null);

  readonly today = computed(() => this.options()?.today ?? this.serverToday() ?? localToday());
  readonly rangeError = computed(() => validateRange(this.filter().from, this.filter().to));

  constructor() {
    // switchMap drops the in-flight request when the filter changes, so a slower older response can never overwrite a newer one.
    this.refresh$
      .pipe(
        switchMap(() => this.fetch()),
        takeUntilDestroyed()
      )
      .subscribe((result) => {
        if ('data' in result) {
          this.data.set(result.data);
          this.error.set(null);
          if (this.serverDates) {
            this.serverDates = false;
            this.serverToday.set(result.data.filter.to);
            // Only the dates come from the server; anything the user picked while the first request was in flight stays.
            const defaults = defaultFilter(result.data.filter.to);
            this.filter.update((current) => ({ ...current, from: defaults.from, to: defaults.to }));
          }
        } else {
          this.error.set('Could not load analytics. Check your connection and try again.');
        }
        this.loading.set(false);
      });

    effect(() => {
      if (!this.auth.currentUser()) {
        untracked(() => this.clear());
      }
    });
  }

  /** Opens the dashboard. The first visit loads the scoped filter options and applies the default filters; later visits
   * keep the chosen filters and just reload. */
  start(): void {
    if (this.started) {
      this.refresh();
      return;
    }

    this.started = true;
    const session = this.session;
    this.loading.set(true);
    this.error.set(null);
    this.http
      .get<TeamFilterOptions>(`${this.baseUrl}/filters`)
      .pipe(catchError(() => of(null)))
      .subscribe((options) => {
        if (session !== this.session) {
          return; // signed out (or reset) while the request was in flight
        }
        this.options.set(options ? { ...options, departments: [], managers: [] } : null);
        if (options) {
          this.filter.set(defaultFilter(options.today));
        } else {
          this.serverDates = true;
        }
        this.refresh();
      });
  }

  update(patch: Partial<AnalyticsFilterState>): void {
    this.filter.update((current) => ({ ...current, ...patch }));
    if (this.rangeError() === null) {
      this.refresh();
    }
  }

  reset(): void {
    this.filter.set(defaultFilter(this.today()));
    this.refresh();
  }

  refresh(): void {
    if (this.rangeError() !== null) {
      return;
    }
    this.refresh$.next();
  }

  clear(): void {
    this.session++;
    this.started = false;
    this.serverDates = false;
    this.serverToday.set(null);
    this.options.set(null);
    this.data.set(null);
    this.error.set(null);
    this.loading.set(false);
    this.filter.set(defaultFilter(localToday()));
  }

  private fetch(): Observable<FetchResult> {
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams();
    for (const [key, value] of Object.entries(toQueryParams(this.filter()))) {
      if (this.serverDates && (key === 'from' || key === 'to')) {
        continue;
      }
      params = params.set(key, value);
    }

    return this.http.get<TeamAnalytics>(this.baseUrl, { params }).pipe(
      map((data): FetchResult => ({ data })),
      catchError(() => of<FetchResult>({ failed: true }))
    );
  }
}
