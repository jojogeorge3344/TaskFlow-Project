import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateDepartmentRequest, Department, DepartmentMembers, DepartmentWithCount, UpdateDepartmentMembersRequest, UpdateDepartmentRequest } from '../models/department.model';

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

  create(request: CreateDepartmentRequest): Observable<Department> {
    return this.http.post<Department>(this.baseUrl, request);
  }

  update(id: number, request: UpdateDepartmentRequest): Observable<Department> {
    return this.http.put<Department>(`${this.baseUrl}/${id}`, request);
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getMembers(id: number): Observable<DepartmentMembers> {
    return this.http.get<DepartmentMembers>(`${this.baseUrl}/${id}/members`);
  }

  /** Replaces the department's managers and employees in one call. */
  replaceMembers(id: number, request: UpdateDepartmentMembersRequest): Observable<DepartmentMembers> {
    return this.http.put<DepartmentMembers>(`${this.baseUrl}/${id}/members`, request);
  }
}
