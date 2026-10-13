import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ActivityEntry,
  AddCommentRequest,
  ReassignTaskRequest,
  SetRecipientsRequest,
  TaskCard,
  TaskComment,
  TaskDetails,
  TaskRecipient,
  UpdateTaskRequest,
  UpdateTaskStatusRequest
} from '../../core/models';

export interface ActivityFilter {
  eventType?: string;
  actorUserId?: number;
  from?: string;
  to?: string;
  search?: string;
}

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/tasks';

  /** Flat list of every task the caller may access; the board groups it client-side. */
  getBoard(): Observable<TaskCard[]> {
    return this.http.get<TaskCard[]>(`${this.baseUrl}/board`);
  }

  getById(id: number): Observable<TaskDetails> {
    return this.http.get<TaskDetails>(`${this.baseUrl}/${id}`);
  }

  update(id: number, request: UpdateTaskRequest): Observable<TaskDetails> {
    return this.http.put<TaskDetails>(`${this.baseUrl}/${id}`, request);
  }

  changeStatus(id: number, request: UpdateTaskStatusRequest): Observable<TaskCard> {
    return this.http.patch<TaskCard>(`${this.baseUrl}/${id}/status`, request);
  }

  reassign(id: number, request: ReassignTaskRequest): Observable<TaskDetails> {
    return this.http.post<TaskDetails>(`${this.baseUrl}/${id}/reassign`, request);
  }

  getRecipients(id: number): Observable<TaskRecipient[]> {
    return this.http.get<TaskRecipient[]>(`${this.baseUrl}/${id}/recipients`);
  }

  setRecipients(id: number, request: SetRecipientsRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}/recipients`, request);
  }

  getComments(id: number): Observable<TaskComment[]> {
    return this.http.get<TaskComment[]>(`${this.baseUrl}/${id}/comments`);
  }

  addComment(id: number, request: AddCommentRequest): Observable<TaskComment> {
    return this.http.post<TaskComment>(`${this.baseUrl}/${id}/comments`, request);
  }

  getActivity(id: number, filter: ActivityFilter = {}): Observable<ActivityEntry[]> {
    let params = new HttpParams();
    if (filter.eventType) {
      params = params.set('eventType', filter.eventType);
    }
    if (filter.actorUserId !== undefined && filter.actorUserId !== null) {
      params = params.set('actorUserId', filter.actorUserId.toString());
    }
    if (filter.from) {
      params = params.set('from', filter.from);
    }
    if (filter.to) {
      params = params.set('to', filter.to);
    }
    if (filter.search) {
      params = params.set('search', filter.search);
    }
    return this.http.get<ActivityEntry[]>(`${this.baseUrl}/${id}/activity`, { params });
  }
}