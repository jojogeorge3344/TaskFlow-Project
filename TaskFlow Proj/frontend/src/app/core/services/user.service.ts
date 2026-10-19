import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateUserRequest, UpdateUserRequest, User, UserStatusRequest } from '../models';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/users';

  getAll(params?: { search?: string; departmentId?: number; role?: string; isActive?: boolean }): Observable<User[]> {
    let httpParams = new HttpParams();
    if (params?.search) {
      httpParams = httpParams.set('search', params.search);
    }
    if (params?.departmentId !== undefined && params?.departmentId !== null) {
      httpParams = httpParams.set('departmentId', params.departmentId.toString());
    }
    if (params?.role) {
      httpParams = httpParams.set('role', params.role);
    }
    if (params?.isActive !== undefined && params?.isActive !== null) {
      httpParams = httpParams.set('isActive', params.isActive.toString());
    }
    return this.http.get<User[]>(this.baseUrl, { params: httpParams });
  }

  getById(id: number): Observable<User> {
    return this.http.get<User>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateUserRequest): Observable<User> {
    return this.http.post<User>(this.baseUrl, request);
  }

  update(id: number, request: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${this.baseUrl}/${id}`, request);
  }

  updateStatus(id: number, request: UserStatusRequest): Observable<User> {
    return this.http.patch<User>(`${this.baseUrl}/${id}/status`, request);
  }
}
