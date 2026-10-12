import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminDashboardStats, EmployeeDashboardStats, ManagerDashboardStats } from '../models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/dashboard';

  getAdminStats(): Observable<AdminDashboardStats> {
    return this.http.get<AdminDashboardStats>(`${this.baseUrl}/admin`);
  }

  getManagerStats(): Observable<ManagerDashboardStats> {
    return this.http.get<ManagerDashboardStats>(`${this.baseUrl}/manager`);
  }

  getEmployeeStats(): Observable<EmployeeDashboardStats> {
    return this.http.get<EmployeeDashboardStats>(`${this.baseUrl}/employee`);
  }
}
