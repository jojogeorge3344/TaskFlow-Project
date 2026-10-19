import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AccessRole, PermissionInfo, SaveRoleRequest } from '../models';

/** Roles and the permission catalog (Permissions Management). Administrator only. */
@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  list(): Observable<AccessRole[]> {
    return this.http.get<AccessRole[]>(`${this.baseUrl}/roles`);
  }

  permissions(): Observable<PermissionInfo[]> {
    return this.http.get<PermissionInfo[]>(`${this.baseUrl}/permissions`);
  }

  create(request: SaveRoleRequest): Observable<AccessRole> {
    return this.http.post<AccessRole>(`${this.baseUrl}/roles`, request);
  }

  update(id: number, request: SaveRoleRequest): Observable<AccessRole> {
    return this.http.put<AccessRole>(`${this.baseUrl}/roles/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/roles/${id}`);
  }
}
