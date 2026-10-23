import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, of, switchMap } from 'rxjs';
import { defaultFilter, localToday, toQueryParams, validateRange } from '../analytics/analytics-filter';
import { AdminAnalytics, AnalyticsFilterOptions, AnalyticsFilterState } from '../models';

type FetchResult = { data: AdminAnalytics } | { failed: true };

@Injectable({ providedIn: 'root' })
export class DashboardAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/dashboard/admin/analytics';
  private readonly refresh$ = new Subject<void>();

  readonly filter = signal<AnalyticsFilterState>(defaultFilter(localToday()));
  readonly options = signal<AnalyticsFilterOptions | null>(null);
  readonly data = signal<AdminAnalytics | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly today = computed(() => this.options()?.today ?? localToday());
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
        } else {
          this.error.set('Could not load analytics. Check your connection and try again.');
        }
        this.loading.set(false);
      });
  }

  /** Loads the filter options (which carry today's date in the application timezone), then the analytics. */
  start(): void {
    this.loading.set(true);
    this.error.set(null);
    this.http
      .get<AnalyticsFilterOptions>(`${this.baseUrl}/filters`)
      .pipe(catchError(() => of(null)))
      .subscribe((options) => {
        this.options.set(options);
        this.filter.set(defaultFilter(this.today()));
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

  private fetch(): Observable<FetchResult> {
    this.loading.set(true);
    this.error.set(null);

    let params = new HttpParams();
    for (const [key, value] of Object.entries(toQueryParams(this.filter()))) {
      params = params.set(key, value);
    }

    return this.http.get<AdminAnalytics>(this.baseUrl, { params }).pipe(
      map((data): FetchResult => ({ data })),
      catchError(() => of<FetchResult>({ failed: true }))
    );
  }
}
