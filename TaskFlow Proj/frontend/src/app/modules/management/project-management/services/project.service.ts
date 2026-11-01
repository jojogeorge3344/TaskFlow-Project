import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateProjectRequest, Project, ProjectMember, ProjectMemberInput, ProjectTask, ProjectTaskFilter, ProjectWithCount, UpdateProjectRequest } from '../models/project.model';
import { User } from '../../employee-management/models/user.model';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/projects';

  getAll(): Observable<ProjectWithCount[]> {
    return this.http.get<ProjectWithCount[]>(this.baseUrl);
  }

  getById(id: number): Observable<Project> {
    return this.http.get<Project>(`${this.baseUrl}/${id}`);
  }

  getEmployees(id: number): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/${id}/employees`);
  }

  getMembers(id: number): Observable<ProjectMember[]> {
    return this.http.get<ProjectMember[]>(`${this.baseUrl}/${id}/members`);
  }

  /** The project's tasks, soonest deadline first; filters combine with AND. */
  getTasks(id: number, filter: ProjectTaskFilter = {}): Observable<ProjectTask[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<ProjectTask[]>(`${this.baseUrl}/${id}/tasks`, { params });
  }

  /** Replaces the project's whole team. All-or-nothing: if anyone would pass 100% in total, nothing is saved (400). */
  replaceMembers(id: number, members: ProjectMemberInput[]): Observable<ProjectMember[]> {
    return this.http.put<ProjectMember[]>(`${this.baseUrl}/${id}/members`, { members });
  }

  create(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(this.baseUrl, request);
  }

  update(id: number, request: UpdateProjectRequest): Observable<Project> {
    return this.http.put<Project>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
