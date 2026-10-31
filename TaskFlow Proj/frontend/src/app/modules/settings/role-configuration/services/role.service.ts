import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AccessRole, PermissionInfo, SaveRoleRequest } from '../models/role.model';

/** Roles and the permission catalog (Permissions Management). Administrator only. */
@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1';

  list(): Observable<AccessRole[]> {
    return this.http.get<AccessRole[]>(`${this.baseUrl}/roles`);
  }

  /** Roles the employee form and filter offer. Needs "Assign roles" and not Role Configuration. */
  assignable(): Observable<AccessRole[]> {
    return this.http
      .get<{ id: number; name: string; isSystem: boolean }[]>(`${this.baseUrl}/users/roles`)
      .pipe(map((roles) => roles.map((role) => ({ ...role, userCount: 0, permissionKeys: [] }))));
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
