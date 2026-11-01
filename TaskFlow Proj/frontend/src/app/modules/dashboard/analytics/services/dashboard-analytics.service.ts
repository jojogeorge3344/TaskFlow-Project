import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, of, switchMap } from 'rxjs';
import { defaultFilter, localToday, toQueryParams, validateRange } from '../../../../shared/utils/analytics-filter';
import { AuthService } from '../../../../shared/auth/auth.service';
import { AdminAnalytics, AnalyticsFilterOptions, AnalyticsFilterState } from '../models/analytics.model';
import { FetchResult } from './dashboard-analytics.model';

@Injectable({ providedIn: 'root' })
export class DashboardAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = '/api/v1/dashboard/admin/analytics';
  private readonly refresh$ = new Subject<void>();
  private started = false;
  private serverDates = false;

  readonly filter = signal<AnalyticsFilterState>(defaultFilter(localToday()));
  readonly allEmployees = signal(false);
  readonly options = signal<AnalyticsFilterOptions | null>(null);
  readonly data = signal<AdminAnalytics | null>(null);
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
            this.filter.set(defaultFilter(result.data.filter.to));
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

  /** Opens the dashboard. The first visit loads the options (which carry today's date in the application timezone)
   * and applies the default filters; later visits keep the chosen filters and just reload. */
  start(): void {
    if (this.started) {
      this.refresh();
      return;
    }

    this.started = true;
    this.loading.set(true);
    this.error.set(null);
    this.http
      .get<AnalyticsFilterOptions>(`${this.baseUrl}/filters`)
      .pipe(catchError(() => of(null)))
      .subscribe((options) => {
        this.options.set(options);
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

  setAllEmployees(value: boolean): void {
    this.allEmployees.set(value);
    this.refresh();
  }

  reset(): void {
    this.filter.set(defaultFilter(this.today()));
    this.allEmployees.set(false);
    this.refresh();
  }

  refresh(): void {
    if (this.rangeError() !== null) {
      return;
    }
    this.refresh$.next();
  }

  clear(): void {
    this.started = false;
    this.serverDates = false;
    this.serverToday.set(null);
    this.options.set(null);
    this.data.set(null);
    this.error.set(null);
    this.loading.set(false);
    this.allEmployees.set(false);
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
    if (this.allEmployees()) {
      params = params.set('allEmployees', 'true');
    }

    return this.http.get<AdminAnalytics>(this.baseUrl, { params }).pipe(
      map((data): FetchResult => ({ data })),
      catchError(() => of<FetchResult>({ failed: true }))
    );
  }
}
