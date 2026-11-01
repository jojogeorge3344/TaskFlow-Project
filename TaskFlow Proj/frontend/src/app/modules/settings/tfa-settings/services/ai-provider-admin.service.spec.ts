import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AddAiModelResult } from '../../../assistant/models/assistant.model';
import { AiProviderAdminService } from './ai-provider-admin.service';

describe('AiProviderAdminService', () => {
  let service: AiProviderAdminService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AiProviderAdminService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts a new model to the provider it belongs to', () => {
    let result: AddAiModelResult | undefined;
    service.addModel('Gemini', { modelName: 'gemini-2.5-flash-lite', apiKey: 'AIza-key' }).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/assistant/providers/Gemini/models');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ modelName: 'gemini-2.5-flash-lite', apiKey: 'AIza-key' });
    request.flush({ ok: false, message: 'Google Gemini did not accept the API key.', failureKind: 'InvalidKey', providers: null });

    expect(result?.ok).toBeFalse();
    expect(result?.providers).toBeNull();
  });

  it('switches TFA to a listed model', () => {
    service.useModel('Gemini', 'gemini-3.5-flash').subscribe();

    const request = http.expectOne('/api/v1/assistant/providers/Gemini/models/use');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ modelName: 'gemini-3.5-flash' });
    request.flush([]);
  });

  it('edits a model by its current name', () => {
    service.editModel('Gemini', 'gemini-2.5-pro', { modelName: 'gemini-2.5-flash' }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === '/api/v1/assistant/providers/Gemini/models');
    expect(request.request.method).toBe('PUT');
    expect(request.request.params.get('name')).toBe('gemini-2.5-pro');
    expect(request.request.body).toEqual({ modelName: 'gemini-2.5-flash' });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null, providers: [] });
  });

  it('tests a listed model, or the saved settings when no model is given', () => {
    service.test('Gemini', 'gemini-2.5-pro').subscribe();
    service.test('Ollama').subscribe();

    const [withModel, saved] = http.match((candidate) => candidate.url.endsWith('/test'));
    expect(withModel.request.body).toEqual({ modelName: 'gemini-2.5-pro' });
    expect(saved.request.url).toBe('/api/v1/assistant/providers/Ollama/test');
    expect(saved.request.body).toEqual({});
    withModel.flush({ ok: true, message: 'The connection works.', failureKind: null });
    saved.flush({ ok: true, message: 'The connection works.', failureKind: null });
  });
});
