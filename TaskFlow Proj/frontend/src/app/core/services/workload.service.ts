import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { EmployeeWorkload } from '../models';

/** Per-employee task load for the Employee Workload screen (Screen 16); Manager/Administrator only. */
@Injectable({ providedIn: 'root' })
export class WorkloadService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/workload';

  getWorkload(projectId: number | null = null): Observable<EmployeeWorkload[]> {
    let params = new HttpParams();
    if (projectId !== null) {
      params = params.set('projectId', String(projectId));
    }
    return this.http.get<EmployeeWorkload[]>(this.baseUrl, { params });
  }
}
