import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { AiProviderConfig, AiProviderTestResult } from '../../core/models';
import { AiProviderSettingsComponent } from './ai-provider-settings.component';

const CONFIG_URL = '/api/v1/assistant/providers/config';

function config(overrides: Partial<Record<'Ollama' | 'Gemini' | 'OpenAI', Partial<AiProviderConfig>>> = {}): AiProviderConfig[] {
  return [
    {
      kind: 'Ollama', displayName: 'Ollama', locality: 'Local', requiresKey: false, isEnabled: true, modelName: 'llama3.1:8b',
      baseUrl: 'http://localhost:11434', keySet: false, keyLast4: null, isConfigured: true, ...overrides.Ollama
    },
    {
      kind: 'Gemini', displayName: 'Google Gemini', locality: 'Cloud', requiresKey: true, isEnabled: true, modelName: 'gemini-2.0-flash',
      baseUrl: null, keySet: true, keyLast4: '1234', isConfigured: true, ...overrides.Gemini
    },
    {
      kind: 'OpenAI', displayName: 'OpenAI GPT', locality: 'Cloud', requiresKey: true, isEnabled: false, modelName: 'gpt-4o-mini',
      baseUrl: null, keySet: false, keyLast4: null, isConfigured: false, ...overrides.OpenAI
    }
  ];
}

describe('AiProviderSettingsComponent', () => {
  let fixture: ComponentFixture<AiProviderSettingsComponent>;
  let http: HttpTestingController;
  const user = signal<AuthUser | null>(null);

  function asRole(role: AuthUser['role']): void {
    user.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.local', role, employeeCode: null });
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function card(kind: string): HTMLElement {
    return el().querySelector(`[data-provider="${kind}"]`) as HTMLElement;
  }

  function field(kind: string, name: string): HTMLInputElement {
    return card(kind).querySelector(`[data-field="${name}"]`) as HTMLInputElement;
  }

  function enter(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function click(selector: string, within: HTMLElement = el()): void {
    (within.querySelector(selector) as HTMLElement).click();
    fixture.detectChanges();
  }

  async function create(role: AuthUser['role'] = 'Administrator', load: AiProviderConfig[] | null = config()): Promise<void> {
    asRole(role);
    await TestBed.configureTestingModule({
      imports: [AiProviderSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { currentUser: user.asReadonly() } }]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AiProviderSettingsComponent);
    fixture.detectChanges();
    if (load && role === 'Administrator') {
      http.expectOne(CONFIG_URL).flush(load);
      fixture.detectChanges();
    }
  }

  afterEach(() => http.verify());

  it('is not shown to non-administrators and does not even ask the server for the configuration', async () => {
    await create('Manager');

    expect(el().querySelector('[data-section="ai-assistant"]')).toBeNull();
    http.expectNone(CONFIG_URL);
  });

  it('shows a card per provider with its locality, status, model and the free-tier notes', async () => {
    await create();

    expect(el().textContent).toContain('OpenAI GPT has no free tier');
    expect(el().textContent).toContain('sent to them');
    expect(card('Ollama').textContent).toContain('Local');
    expect(card('Gemini').textContent).toContain('Cloud');
    expect(card('Ollama').querySelector('[data-status="configured"]')!.textContent).toContain('Ready');
    expect(card('OpenAI').querySelector('[data-status="configured"]')!.textContent).toContain('Not configured');
    expect(field('Gemini', 'modelName').value).toBe('gemini-2.0-flash');
    expect(field('Ollama', 'baseUrl').value).toBe('http://localhost:11434');
    expect(field('Ollama', 'apiKey')).toBeNull();
  });

  it('never shows a saved key: the box is empty and only "ends …1234" is said', async () => {
    await create();

    expect(field('Gemini', 'apiKey').value).toBe('');
    expect(field('Gemini', 'apiKey').type).toBe('password');
    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('ends …1234');
    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('Leave the box empty to keep it');
    expect(el().innerHTML).not.toContain('sk-');
  });

  it('keeps the stored key when the key box is left empty and only the model changes', async () => {
    await create();
    enter(field('Gemini', 'modelName'), 'gemini-2.5-flash');

    click('[data-action="save-ai"]');

    const request = http.expectOne(CONFIG_URL);
    expect(request.request.method).toBe('PUT');
    const gemini = request.request.body.providers.find((p: { kind: string }) => p.kind === 'Gemini');
    expect(gemini.modelName).toBe('gemini-2.5-flash');
    expect(gemini.apiKey).toBeUndefined();
    expect(gemini.clearKey).toBeUndefined();
    request.flush(config({ Gemini: { modelName: 'gemini-2.5-flash' } }));
  });

  it('sends a new key once and clears the box after saving, so it is never shown again', async () => {
    await create();
    enter(field('OpenAI', 'apiKey'), 'sk-proj-brand-new-key-9999');
    click('[data-field="isEnabled"]', card('OpenAI'));

    click('[data-action="save-ai"]');
    const request = http.expectOne(CONFIG_URL);
    const openAi = request.request.body.providers.find((p: { kind: string }) => p.kind === 'OpenAI');
    expect(openAi.apiKey).toBe('sk-proj-brand-new-key-9999');
    expect(openAi.isEnabled).toBeTrue();
    request.flush(config({ OpenAI: { isEnabled: true, keySet: true, keyLast4: '9999', isConfigured: true } }));
    fixture.detectChanges();

    expect(field('OpenAI', 'apiKey').value).toBe('');
    expect(card('OpenAI').querySelector('[data-hint="key"]')!.textContent).toContain('ends …9999');
    expect(el().innerHTML).not.toContain('brand-new-key');
  });

  it('removes a stored key only when asked, and lets the user change their mind', async () => {
    await create();

    click('[data-action="clear-key"]', card('Gemini'));
    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('will be removed');
    click('[data-action="keep-key"]', card('Gemini'));
    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('ends …1234');
    click('[data-action="clear-key"]', card('Gemini'));

    click('[data-action="save-ai"]');
    const request = http.expectOne(CONFIG_URL);
    const gemini = request.request.body.providers.find((p: { kind: string }) => p.kind === 'Gemini');
    expect(gemini.clearKey).toBeTrue();
    expect(gemini.apiKey).toBeUndefined();
    request.flush(config({ Gemini: { keySet: false, keyLast4: null, isConfigured: false } }));
    fixture.detectChanges();

    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('No key saved yet');
  });

  it('does not save invalid input: a blank model, a bad address or a key with spaces', async () => {
    await create();
    enter(field('Ollama', 'modelName'), '   ');
    enter(field('Ollama', 'baseUrl'), 'ftp://nope');
    enter(field('Gemini', 'apiKey'), 'has a space');

    click('[data-action="save-ai"]');

    http.expectNone(CONFIG_URL);
    expect(card('Ollama').querySelector('[data-error="baseUrl"]')).not.toBeNull();
    expect(card('Gemini').querySelector('[data-error="apiKey"]')).not.toBeNull();
  });

  it('shows a server error from saving', async () => {
    await create();
    enter(field('Gemini', 'modelName'), 'something');

    click('[data-action="save-ai"]');
    http.expectOne(CONFIG_URL).flush({ detail: 'Gemini does not accept that.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(el().querySelector('[data-error="ai-save"]')!.textContent).toContain('Gemini does not accept that.');
  });

  describe('test connection', () => {
    function testResult(kind: string, result: AiProviderTestResult): void {
      click('[data-action="test-connection"]', card(kind));
      http.expectOne(`/api/v1/assistant/providers/${kind}/test`).flush(result);
      fixture.detectChanges();
    }

    it('shows success', async () => {
      await create();

      testResult('Ollama', { ok: true, message: 'The connection works.', failureKind: null });

      const result = card('Ollama').querySelector('[data-result]')!;
      expect(result.getAttribute('data-result')).toBe('ok');
      expect(result.textContent).toContain('The connection works.');
    });

    it('shows each kind of failure with its own message', async () => {
      await create();
      const failures: [string, AiProviderTestResult['failureKind'], string][] = [
        ['Gemini', 'RateLimited', 'Google Gemini\'s free limit or rate limit has been reached.'],
        ['Gemini', 'InvalidKey', 'Google Gemini did not accept the API key.'],
        ['Ollama', 'Unreachable', 'Ollama is not running or cannot be reached.'],
        ['Ollama', 'ModelUnavailable', 'The configured model is not available on Ollama.'],
        ['Gemini', 'Timeout', 'Google Gemini took too long to answer.']
      ];

      for (const [kind, failureKind, message] of failures) {
        testResult(kind, { ok: false, message, failureKind });
        const result = card(kind).querySelector('[data-result]')!;
        expect(result.getAttribute('data-result')).toBe(failureKind!);
        expect(result.textContent).toContain(message);
      }
    });

    it('says a provider that is not configured is not configured', async () => {
      await create();

      testResult('OpenAI', { ok: false, message: 'OpenAI GPT is not configured.', failureKind: null });

      expect(card('OpenAI').querySelector('[data-result]')!.textContent).toContain('not configured');
    });

    it('asks to save first when there are unsaved changes, because the test uses the saved settings', async () => {
      await create();
      enter(field('Gemini', 'modelName'), 'changed');

      const button = card('Gemini').querySelector('[data-action="test-connection"]') as HTMLButtonElement;

      expect(button.disabled).toBeTrue();
      expect(button.title).toContain('Save your changes first');
    });

    it('reports a request error as a failed test', async () => {
      await create();

      click('[data-action="test-connection"]', card('Ollama'));
      http.expectOne('/api/v1/assistant/providers/Ollama/test').flush({ detail: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
      fixture.detectChanges();

      expect(card('Ollama').querySelector('[data-result]')!.textContent).toContain('Forbidden');
    });
  });

  it('shows a retry when the configuration cannot be loaded', async () => {
    await create('Administrator', null);
    http.expectOne(CONFIG_URL).flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el().querySelector('[role="alert"]')!.textContent).toContain('Could not load');
    click('[data-action="retry-ai"]');
    http.expectOne(CONFIG_URL).flush(config());
    fixture.detectChanges();

    expect(card('Ollama')).not.toBeNull();
  });
});
