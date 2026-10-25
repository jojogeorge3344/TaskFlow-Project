import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ReportFilter, ReportSummary } from '../models';

/** Filtered task totals and CSV export for the Reports screen (Screen 17); Manager/Administrator only. */
@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/reports';

  getSummary(filter: ReportFilter = {}): Observable<ReportSummary> {
    return this.http.get<ReportSummary>(this.baseUrl, { params: toParams(filter) });
  }

  /** The filtered task rows as a CSV attachment; the browser saves it under the server's file name. */
  exportCsv(filter: ReportFilter = {}): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/export`, { params: toParams(filter), responseType: 'blob' });
  }
}

function toParams(filter: ReportFilter): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
