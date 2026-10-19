import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AiProviderConfig, AiProviderKind, AiProviderTestResult, UpdateAiProvider } from '../models';

/** Administrator-only configuration of the AI providers TFA can use. The API key is sent to the server when it is
 * saved and is never returned: the server only says whether a key is set and its last four characters. */
@Injectable({ providedIn: 'root' })
export class AiProviderAdminService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/assistant/providers';

  getConfig(): Observable<AiProviderConfig[]> {
    return this.http.get<AiProviderConfig[]>(`${this.url}/config`);
  }

  save(providers: UpdateAiProvider[]): Observable<AiProviderConfig[]> {
    return this.http.put<AiProviderConfig[]>(`${this.url}/config`, { providers });
  }

  /** Removes a model an administrator added. The server refuses built-in models and the one saved for the server. */
  removeModel(kind: AiProviderKind, name: string): Observable<AiProviderConfig[]> {
    return this.http.delete<AiProviderConfig[]>(`${this.url}/${kind}/models`, { params: { name } });
  }

  /** Sends one tiny request to the saved provider settings. */
  test(kind: AiProviderKind): Observable<AiProviderTestResult> {
    return this.http.post<AiProviderTestResult>(`${this.url}/${kind}/test`, {});
  }
}
