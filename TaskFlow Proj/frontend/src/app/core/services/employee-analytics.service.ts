import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, of, switchMap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { EmployeeOwnAnalytics, EmployeePeriod } from '../models';
import { FetchResult } from './employee-analytics.model';

/** State for the Employee analytics page. The only input is the period: the server always answers for the signed-in
 * user, so there is no employee, department or project to choose. */
@Injectable({ providedIn: 'root' })
export class EmployeeAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = '/api/v1/dashboard/employee/analytics';
  private readonly refresh$ = new Subject<void>();

  readonly period = signal<EmployeePeriod>('30d');
  readonly data = signal<EmployeeOwnAnalytics | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    // switchMap drops the in-flight request when the period changes, so a slower older response never overwrites a newer one.
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
          this.error.set('Could not load your dashboard. Check your connection and try again.');
        }
        this.loading.set(false);
      });

    effect(() => {
      if (!this.auth.currentUser()) {
        untracked(() => this.clear());
      }
    });
  }

  refresh(): void {
    this.refresh$.next();
  }

  setPeriod(period: EmployeePeriod): void {
    this.period.set(period);
    this.refresh();
  }

  clear(): void {
    this.period.set('30d');
    this.data.set(null);
    this.error.set(null);
    this.loading.set(false);
  }

  private fetch(): Observable<FetchResult> {
    this.loading.set(true);
    this.error.set(null);

    return this.http.get<EmployeeOwnAnalytics>(this.url, { params: new HttpParams().set('period', this.period()) }).pipe(
      map((data): FetchResult => ({ data })),
      catchError(() => of<FetchResult>({ failed: true }))
    );
  }
}
