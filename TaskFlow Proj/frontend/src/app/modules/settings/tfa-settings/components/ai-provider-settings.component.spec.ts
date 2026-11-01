import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthUser } from '../../../../shared/auth/auth.models';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { ToastService } from '../../../../shared/services/toast.service';
import { fakeAuthService, systemPermissions } from '../../../../shared/testing/permissions';
import { AiProviderConfig } from '../../../assistant/models/assistant.model';
import { AiProviderSettingsComponent } from './ai-provider-settings.component';

const BASE = '/api/v1/assistant/providers';
const CONFIG_URL = `${BASE}/config`;

type Kind = 'Ollama' | 'Gemini' | 'OpenAI';

/** TFA uses gemini-2.5-pro (added by an administrator); gemini-2.0-flash was added too; OpenAI has no key. */
function config(overrides: Partial<Record<Kind, Partial<AiProviderConfig>>> = {}): AiProviderConfig[] {
  return [
    {
      providerId: 1, displayName: 'Ollama', apiType: 'Ollama', isBuiltIn: true, locality: 'Local', requiresKey: false, isEnabled: false, modelName: 'llama3.2:3b',
      baseUrl: 'http://localhost:11434', keySet: false, keyLast4: null, isConfigured: false, keyReadable: true,
      models: ['llama3.2:3b', 'llama3.1:8b'], customModels: [], ...overrides.Ollama
    },
    {
      providerId: 2, displayName: 'Google Gemini', apiType: 'Gemini', isBuiltIn: true, locality: 'Cloud', requiresKey: true, isEnabled: true, modelName: 'gemini-2.5-pro',
      baseUrl: null, keySet: true, keyLast4: '1234', isConfigured: true, keyReadable: true,
      models: ['gemini-3.8-flash', 'gemini-2.5-pro', 'gemini-2.0-flash'], customModels: ['gemini-2.5-pro', 'gemini-2.0-flash'], ...overrides.Gemini
    },
    {
      providerId: 3, displayName: 'OpenAI GPT', apiType: 'OpenAI', isBuiltIn: true, locality: 'Cloud', requiresKey: true, isEnabled: false, modelName: 'gpt-4o-mini',
      baseUrl: null, keySet: false, keyLast4: null, isConfigured: false, keyReadable: true,
      models: ['gpt-4o-mini'], customModels: [], ...overrides.OpenAI
    }
  ];
}

describe('AiProviderSettingsComponent', () => {
  let fixture: ComponentFixture<AiProviderSettingsComponent>;
  let http: HttpTestingController;
  let router: Router;
  let toast: ToastService;
  const user = signal<AuthUser | null>(null);

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function row(model: string): HTMLElement {
    return el().querySelector(`[data-model="${model}"]`) as HTMLElement;
  }

  function action(model: string, name: string): HTMLButtonElement | null {
    return row(model).querySelector(`[data-action="${name}"]`);
  }

  function dialog(): HTMLDialogElement {
    return el().querySelector('dialog')!;
  }

  async function create(permissions: string[] = systemPermissions('Administrator'), load: AiProviderConfig[] | null = config()): Promise<void> {
    user.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.com', role: 'Administrator', employeeCode: null, permissions });
    await TestBed.configureTestingModule({
      imports: [AiProviderSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: AuthService, useValue: fakeAuthService(user.asReadonly()) }]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    toast = TestBed.inject(ToastService);
    spyOn(router, 'navigate').and.resolveTo(true);
    spyOn(toast, 'success');
    fixture = TestBed.createComponent(AiProviderSettingsComponent);
    fixture.detectChanges();
    if (load) {
      http.expectOne(CONFIG_URL).flush(load);
      fixture.detectChanges();
    }
  }

  afterEach(() => {
    if (dialog()?.open) {
      dialog().close();
    }
    http.verify();
  });

  it('is not shown without settings.ai.view and does not ask the server for the configuration', async () => {
    await create([Permissions.DashboardAnalytics], null);

    expect(el().querySelector('[data-section="ai-assistant"]')).toBeNull();
    http.expectNone(CONFIG_URL);
  });

  it('says Gemini is the default and that document search needs a Gemini key and sends document text to Google', async () => {
    await create();

    const section = el().querySelector('[data-section="ai-assistant"]')!;
    expect(section.textContent).toContain('Google Gemini is the default');
    const help = section.querySelector('[data-help="document-search"]')!;
    expect(help.textContent).toContain('Document search needs a Google Gemini API key');
    expect(help.textContent).toContain('sent to Google');
  });

  it('lists every model read-only, grouped by provider, with no dropdown, field or Save button', async () => {
    await create();

    const section = el().querySelector('[data-section="ai-assistant"]')!;
    expect(section.querySelector('select')).toBeNull();
    expect(section.querySelector('input')).toBeNull();
    expect(section.querySelector('[data-action="save-ai"]')).toBeNull();
    expect(Array.from(el().querySelectorAll('[data-provider]')).map((group) => group.getAttribute('data-provider'))).toEqual(['1', '2', '3']);
    expect(el().querySelectorAll('[data-model]').length).toBe(6);

    expect(row('gemini-3.8-flash').querySelector('[data-detail="provider"]')?.textContent).toContain('Google Gemini · Cloud');
    expect(row('gemini-3.8-flash').querySelector('[data-detail="origin"]')?.textContent).toContain('Built-in');
    expect(row('gemini-2.0-flash').querySelector('[data-detail="origin"]')?.textContent).toContain('Added by you');
    expect(row('llama3.2:3b').querySelector('[data-detail="key"]')?.textContent).toContain('http://localhost:11434');
    expect(row('gpt-4o-mini').querySelector('[data-detail="key"]')?.textContent).toContain('No key saved');
  });

  it('shows the shared Gemini key on every Gemini row, and marks only the model in use', async () => {
    await create();

    for (const model of ['gemini-3.8-flash', 'gemini-2.5-pro', 'gemini-2.0-flash']) {
      expect(row(model).querySelector('[data-detail="key"]')?.textContent).toContain('ends …1234');
    }
    expect(el().querySelectorAll('[data-status="in-use"]').length).toBe(1);
    expect(row('gemini-2.5-pro').querySelector('[data-status="in-use"]')).toBeTruthy();
    expect(action('gemini-2.5-pro', 'use-model')).toBeNull();
    expect(el().querySelector('[data-using]')?.textContent).toContain('TFA uses gemini-2.5-pro on Google Gemini');
  });

  it('switches TFA to a model with Use this model', async () => {
    await create();

    action('llama3.2:3b', 'use-model')!.click();
    const request = http.expectOne(`${BASE}/1/models/use`);
    expect(request.request.body).toEqual({ modelName: 'llama3.2:3b' });
    request.flush(config({ Ollama: { isEnabled: true, isConfigured: true }, Gemini: { isEnabled: false } }));
    fixture.detectChanges();

    expect(row('llama3.2:3b').querySelector('[data-status="in-use"]')).toBeTruthy();
    expect(row('gemini-2.5-pro').querySelector('[data-status="in-use"]')).toBeNull();
    expect(toast.success).toHaveBeenCalledWith('llama3.2:3b on Ollama is now in use');
  });

  it('disables Use this model with a hint when the provider has no key', async () => {
    await create();

    expect(action('gpt-4o-mini', 'use-model')!.disabled).toBeTrue();
    expect(row('gpt-4o-mini').querySelector('[data-hint="no-key"]')?.textContent).toContain(
      'No key saved for OpenAI GPT yet. Add it on the Add AI model screen.'
    );
  });

  it('tests a row with its own model and shows the result on that row', async () => {
    await create();

    action('gemini-3.8-flash', 'test-model')!.click();
    const request = http.expectOne(`${BASE}/2/test`);
    expect(request.request.body).toEqual({ modelName: 'gemini-3.8-flash' });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null });
    fixture.detectChanges();

    expect(row('gemini-3.8-flash').querySelector('[data-result="ok"]')?.textContent).toContain('The connection works.');
    expect(row('gemini-2.0-flash').querySelector('[data-result]')).toBeNull();
  });

  it('offers Edit and Delete only on added models', async () => {
    await create();

    expect(action('gemini-3.8-flash', 'edit-model')).toBeNull();
    expect(action('gemini-3.8-flash', 'delete-model')).toBeNull();
    expect(action('llama3.2:3b', 'delete-model')).toBeNull();
    expect(action('gemini-2.0-flash', 'edit-model')).toBeTruthy();
    expect(action('gemini-2.0-flash', 'delete-model')).toBeTruthy();
  });

  it('opens the edit screen for an added model that is not in use', async () => {
    await create();

    action('gemini-2.0-flash', 'edit-model')!.click();
    await fixture.whenStable();

    expect(router.navigate).toHaveBeenCalledWith(['/settings/tfa/models', 2, 'gemini-2.0-flash', 'edit']);
  });

  it('says the model in use must be switched first, instead of editing or deleting it', async () => {
    await create();

    action('gemini-2.5-pro', 'delete-model')!.click();
    fixture.detectChanges();
    expect(dialog().open).toBeTrue();
    expect(dialog().textContent).toContain('gemini-2.5-pro is in use by TFA. Switch TFA to another model first, then delete it.');
    expect(dialog().querySelector('[data-action="cancel"]')).toBeNull();
    (dialog().querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    action('gemini-2.5-pro', 'edit-model')!.click();
    fixture.detectChanges();
    expect(dialog().textContent).toContain('then edit it.');
    (dialog().querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(router.navigate).not.toHaveBeenCalled();
    http.expectNone((request) => request.method === 'DELETE');
  });

  it('deletes an added model after confirmation', async () => {
    await create();

    action('gemini-2.0-flash', 'delete-model')!.click();
    fixture.detectChanges();
    expect(dialog().textContent).toContain('Delete gemini-2.0-flash?');
    (dialog().querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    const request = http.expectOne((candidate) => candidate.method === 'DELETE' && candidate.url === `${BASE}/2/models`);
    expect(request.request.params.get('name')).toBe('gemini-2.0-flash');
    request.flush(config({ Gemini: { models: ['gemini-3.8-flash', 'gemini-2.5-pro'], customModels: ['gemini-2.5-pro'] } }));
    fixture.detectChanges();

    expect(row('gemini-2.0-flash')).toBeNull();
    expect(toast.success).toHaveBeenCalledWith('Deleted gemini-2.0-flash');
  });

  it('lists a custom provider after the built-in ones, and drops its group once its last model is deleted', async () => {
    const groq: AiProviderConfig = {
      providerId: 9, displayName: 'Groq', apiType: 'OpenAI', isBuiltIn: false, locality: 'Cloud', requiresKey: false, isEnabled: false,
      modelName: 'llama-3.3-70b-versatile', baseUrl: 'https://api.groq.com/openai/v1', keySet: true, keyLast4: '4321', isConfigured: false,
      keyReadable: true, models: ['llama-3.3-70b-versatile'], customModels: ['llama-3.3-70b-versatile']
    };
    await create(systemPermissions('Administrator'), [...config(), groq]);

    const groups = Array.from(el().querySelectorAll('[data-provider]'));
    expect(groups.map((group) => group.getAttribute('data-provider'))).toEqual(['1', '2', '3', '9']);
    expect(groups[3].getAttribute('data-custom')).toBe('true');
    const row9 = row('llama-3.3-70b-versatile');
    expect(row9.querySelector('[data-detail="provider"]')?.textContent).toContain('Groq · Cloud');
    expect(row9.querySelector('[data-detail="key"]')?.textContent).toContain('https://api.groq.com/openai/v1 · key ends …4321');
    expect(row9.querySelector('[data-detail="origin"]')?.textContent).toContain('Added by you');
    expect(action('llama-3.3-70b-versatile', 'use-model')!.disabled).toBeFalse();

    action('llama-3.3-70b-versatile', 'delete-model')!.click();
    fixture.detectChanges();
    (dialog().querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    const request = http.expectOne((candidate) => candidate.method === 'DELETE' && candidate.url === `${BASE}/9/models`);
    request.flush(config());
    fixture.detectChanges();

    expect(el().querySelector('[data-provider="9"]')).toBeNull();
  });

  it('keeps the model when the delete confirmation is cancelled', async () => {
    await create();

    action('gemini-2.0-flash', 'delete-model')!.click();
    fixture.detectChanges();
    (dialog().querySelector('[data-action="cancel"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    http.expectNone((request) => request.method === 'DELETE');
    expect(row('gemini-2.0-flash')).toBeTruthy();
  });

  it('hides each action without its permission', async () => {
    await create([Permissions.SettingsAiView]);

    expect(el().querySelector('[data-action="use-model"]')).toBeNull();
    expect(el().querySelector('[data-action="test-model"]')).toBeNull();
    expect(el().querySelector('[data-action="edit-model"]')).toBeNull();
    expect(el().querySelector('[data-action="delete-model"]')).toBeNull();
    expect(el().querySelector('[data-action="add-model"]')).toBeNull();
  });

  it('shows the server error when switching fails', async () => {
    await create();

    action('llama3.2:3b', 'use-model')!.click();
    http.expectOne(`${BASE}/1/models/use`).flush({ title: 'Could not switch.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(el().querySelector('[data-error="ai-action"]')).toBeTruthy();
  });
});
