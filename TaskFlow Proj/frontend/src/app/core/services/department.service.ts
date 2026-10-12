import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateDepartmentRequest, Department, DepartmentWithCount, UpdateDepartmentRequest, User } from '../models';

@Injectable({ providedIn: 'root' })
export class DepartmentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/departments';

  getAll(): Observable<DepartmentWithCount[]> {
    return this.http.get<DepartmentWithCount[]>(this.baseUrl);
  }

  getById(id: number): Observable<Department> {
    return this.http.get<Department>(`${this.baseUrl}/${id}`);
  }

  getEmployees(id: number): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/${id}/employees`);
  }

  create(request: CreateDepartmentRequest): Observable<Department> {
    return this.http.post<Department>(this.baseUrl, request);
  }

  update(id: number, request: UpdateDepartmentRequest): Observable<Department> {
    return this.http.put<Department>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
