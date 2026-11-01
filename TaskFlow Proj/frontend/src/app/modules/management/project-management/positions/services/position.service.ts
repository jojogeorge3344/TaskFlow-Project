import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Position } from '../../models/project.model';

/** The positions people hold on a project (QA, Developer, ...). A label only; it grants no access. */
@Injectable({ providedIn: 'root' })
export class PositionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/positions';

  getAll(): Observable<Position[]> {
    return this.http.get<Position[]>(this.baseUrl);
  }

  /** Creates the position, or returns the existing one when the name matches ignoring case. */
  getOrCreate(name: string): Observable<Position> {
    return this.http.post<Position>(this.baseUrl, { name });
  }
}
