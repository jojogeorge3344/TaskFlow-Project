import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AddAiModelRequest, AddAiModelResult, AiProviderConfig, AiProviderTestResult, UpdateAiProvider } from '../../../assistant/models/assistant.model';

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

  /** Removes a model an administrator added. The server refuses built-in models and the one TFA uses. */
  removeModel(providerId: number, name: string): Observable<AiProviderConfig[]> {
    return this.http.delete<AiProviderConfig[]>(`${this.url}/${providerId}/models`, { params: { name } });
  }

  /** Sends one tiny request to the saved provider settings, or, with `modelName`, to that listed model with the saved
   * key or address (also for a provider TFA does not use). Nothing is saved. */
  test(providerId: number, modelName?: string): Observable<AiProviderTestResult> {
    return this.http.post<AiProviderTestResult>(`${this.url}/${providerId}/test`, modelName ? { modelName } : {});
  }

  /** "Use this model": makes a listed model the one TFA uses and switches the other providers off. */
  useModel(providerId: number, modelName: string): Observable<AiProviderConfig[]> {
    return this.http.post<AiProviderConfig[]>(`${this.url}/${providerId}/models/use`, { modelName });
  }

  /** The edit screen: tests the typed values and, only if the test passes, renames the model and saves the key or
   * address. `clearKey` removes the key without a test. A failed test comes back as `ok: false` with nothing saved. */
  editModel(providerId: number, name: string, body: AddAiModelRequest): Observable<AddAiModelResult> {
    return this.http.put<AddAiModelResult>(`${this.url}/${providerId}/models`, body, { params: { name } });
  }

  /** The Add AI model page: tests the typed model and key (or Ollama address) and, only if the test passes, saves the
   * model and makes it the one TFA uses. A failed test comes back as `ok: false` with nothing saved. */
  addModel(body: AddAiModelRequest): Observable<AddAiModelResult> {
    return this.http.post<AddAiModelResult>(`${this.url}/models`, body);
  }
}
