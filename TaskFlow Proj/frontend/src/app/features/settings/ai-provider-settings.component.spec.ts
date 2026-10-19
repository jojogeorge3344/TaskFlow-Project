import { provideHttpClient } from '@angular/common/http';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
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
      baseUrl: 'http://localhost:11434', keySet: false, keyLast4: null, isConfigured: true, keyReadable: true,
      models: ['llama3.2:3b', 'llama3.1:8b'], ...overrides.Ollama
    },
    {
      kind: 'Gemini', displayName: 'Google Gemini', locality: 'Cloud', requiresKey: true, isEnabled: true, modelName: 'gemini-2.0-flash',
      baseUrl: null, keySet: true, keyLast4: '1234', isConfigured: true, keyReadable: true,
      models: ['gemini-2.0-flash', 'gemini-2.5-flash'], ...overrides.Gemini
    },
    {
      kind: 'OpenAI', displayName: 'OpenAI GPT', locality: 'Cloud', requiresKey: true, isEnabled: false, modelName: 'gpt-4o-mini',
      baseUrl: null, keySet: false, keyLast4: null, isConfigured: false, keyReadable: true,
      models: ['gpt-4o-mini', 'gpt-4o'], ...overrides.OpenAI
    }
  ];
}

/** The configuration as the server returns it once `kind` is the server TFA uses: only that one is enabled. */
function activeConfig(kind: 'Ollama' | 'Gemini' | 'OpenAI', overrides: Parameters<typeof config>[0] = {}): AiProviderConfig[] {
  const only = (name: 'Ollama' | 'Gemini' | 'OpenAI'): Partial<AiProviderConfig> => ({
    isEnabled: name === kind,
    isConfigured: name === kind && (name === 'Ollama' || name === 'Gemini' ? true : (overrides.OpenAI?.keySet ?? false)),
    ...overrides[name]
  });
  return config({ Ollama: only('Ollama'), Gemini: only('Gemini'), OpenAI: only('OpenAI') });
}

describe('AiProviderSettingsComponent', () => {
  let fixture: ComponentFixture<AiProviderSettingsComponent>;
  let http: HttpTestingController;
  const user = signal<AuthUser | null>(null);

  function asRole(role: AuthUser['role']): void {
    user.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.local', role, employeeCode: null, permissions: systemPermissions(role) });
  }

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function card(kind: string): HTMLElement {
    return el().querySelector(`[data-provider="${kind}"]`) as HTMLElement;
  }

  function modelSelect(): HTMLSelectElement {
    return el().querySelector('[data-field="model-select"]') as HTMLSelectElement;
  }

  function serverSelect(): HTMLSelectElement {
    return el().querySelector('[data-field="server-select"]') as HTMLSelectElement;
  }

  function newModelInput(): HTMLInputElement {
    return el().querySelector('[data-field="newModel"]') as HTMLInputElement;
  }

  /** Picks a server in the Server dropdown. The Model dropdown follows, and that server's fields are shown. */
  function choose(kind: string): void {
    serverSelect().value = kind;
    serverSelect().dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  /** Picks a model in the Model dropdown, which lists the models of every server. The Server dropdown follows. */
  function pickModel(kind: string, model: string): void {
    modelSelect().value = `${kind}|${model}`;
    modelSelect().dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  /** The models listed under one server in the Model dropdown. */
  function modelsOf(displayName: string): string[] {
    const group = Array.from(modelSelect().querySelectorAll('optgroup')).find((g) => g.label === displayName)!;
    return optionTexts(group as unknown as HTMLElement);
  }

  function optionTexts(select: HTMLElement): string[] {
    return Array.from(select.querySelectorAll('option')).map((option) => option.textContent!.trim());
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
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: fakeAuthService(user.asReadonly()) }]
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

  it('offers every server in a dropdown, with where it runs, and the free-tier notes', async () => {
    await create();

    expect(el().textContent).toContain('OpenAI GPT has no free tier');
    expect(el().textContent).toContain('sent to them');
    expect(optionTexts(serverSelect())).toEqual([
      'Ollama · http://localhost:11434',
      'Google Gemini · generativelanguage.googleapis.com',
      'OpenAI GPT · api.openai.com'
    ]);
  });

  it("shows only the chosen server's fields: the address for Ollama, the API key for a cloud server", async () => {
    await create();

    // The first server is shown to begin with: Ollama has an address and no key.
    expect(card('Ollama')).not.toBeNull();
    expect(card('Gemini')).toBeNull();
    expect(field('Ollama', 'baseUrl').value).toBe('http://localhost:11434');
    expect(field('Ollama', 'apiKey')).toBeNull();
    expect(card('Ollama').querySelector('[data-status="configured"]')!.textContent).toContain('In use');
    expect(card('Ollama').textContent).toContain('Local');

    choose('Gemini');
    expect(card('Ollama')).toBeNull();
    expect(card('Gemini').textContent).toContain('Cloud');
    expect(field('Gemini', 'apiKey')).not.toBeNull();
    expect(field('Gemini', 'baseUrl')).toBeNull();

    choose('OpenAI');
    expect(card('OpenAI').querySelector('[data-status="configured"]')!.textContent).toContain('Needs an API key');
    expect(field('OpenAI', 'apiKey')).not.toBeNull();
  });

  it('lists the models kept in the database under their server, each with "Add new model…" last', async () => {
    await create();

    expect(modelsOf('Ollama')).toEqual(['llama3.2:3b', 'llama3.1:8b', 'Add new model…']);
    expect(modelsOf('Google Gemini')).toEqual(['gemini-2.0-flash', 'gemini-2.5-flash', 'Add new model…']);
    expect(modelsOf('OpenAI GPT')).toEqual(['gpt-4o-mini', 'gpt-4o', 'Add new model…']);
    expect(newModelInput()).toBeNull();
  });

  it('still lists the chosen model of every server when the API sends no model list', async () => {
    const withoutLists = config().map((provider) => {
      const { models, ...rest } = provider;
      void models;
      return rest as AiProviderConfig;
    });
    await create('Administrator', withoutLists);

    expect(modelsOf('Ollama')).toEqual(['llama3.1:8b', 'Add new model…']);
    expect(modelsOf('Google Gemini')).toEqual(['gemini-2.0-flash', 'Add new model…']);
    expect(modelsOf('OpenAI GPT')).toEqual(['gpt-4o-mini', 'Add new model…']);
  });

  it('choosing a model selects the server it belongs to', async () => {
    await create();
    expect(serverSelect().value).toBe('Ollama');

    pickModel('OpenAI', 'gpt-4o');

    expect(serverSelect().value).toBe('OpenAI');
    expect(modelSelect().value).toBe('OpenAI|gpt-4o');
    expect(card('OpenAI')).not.toBeNull();
    expect(card('Ollama')).toBeNull();
    expect(field('OpenAI', 'apiKey')).not.toBeNull();

    pickModel('Ollama', 'llama3.2:3b');

    expect(serverSelect().value).toBe('Ollama');
    expect(field('Ollama', 'baseUrl')).not.toBeNull();
  });

  it("choosing a server selects that server's model", async () => {
    await create();
    expect(modelSelect().value).toBe('Ollama|llama3.1:8b');

    choose('Gemini');
    expect(modelSelect().value).toBe('Gemini|gemini-2.0-flash');

    choose('OpenAI');
    expect(modelSelect().value).toBe('OpenAI|gpt-4o-mini');
  });

  it("remembers each server's chosen model when switching between servers", async () => {
    await create();

    pickModel('Gemini', 'gemini-2.5-flash');
    choose('OpenAI');
    expect(modelSelect().value).toBe('OpenAI|gpt-4o-mini');
    choose('Gemini');

    expect(modelSelect().value).toBe('Gemini|gemini-2.5-flash');
  });

  it('shows the Ollama address as it is typed, in the Server dropdown', async () => {
    await create();

    enter(field('Ollama', 'baseUrl'), 'http://gpu-box:11434');

    expect(optionTexts(serverSelect())[0]).toBe('Ollama · http://gpu-box:11434');
  });

  it('adds a new model: the typed name is saved, and is selected and offered from then on', async () => {
    await create();
    pickModel('Gemini', '__add__');
    expect(serverSelect().value).toBe('Gemini');
    expect(newModelInput()).not.toBeNull();
    enter(newModelInput(), '  gemini-3-pro  ');

    click('[data-action="save-ai"]');

    const request = http.expectOne(CONFIG_URL);
    const gemini = request.request.body.providers.find((p: { kind: string }) => p.kind === 'Gemini');
    expect(gemini.modelName).toBe('gemini-3-pro');
    request.flush(config({ Gemini: { modelName: 'gemini-3-pro', models: ['gemini-2.0-flash', 'gemini-2.5-flash', 'gemini-3-pro'] } }));
    fixture.detectChanges();

    expect(modelsOf('Google Gemini')).toEqual(['gemini-2.0-flash', 'gemini-2.5-flash', 'gemini-3-pro', 'Add new model…']);
    expect(modelSelect().value).toBe('Gemini|gemini-3-pro');
    expect(newModelInput()).toBeNull();
  });

  it('does not save "Add new model…" with no name, and shows the problem even if another server is on screen', async () => {
    await create();
    pickModel('Gemini', '__add__');
    choose('OpenAI');

    click('[data-action="save-ai"]');

    http.expectNone(CONFIG_URL);
    expect(serverSelect().value).toBe('Gemini');
    expect(el().querySelector('[data-error="newModel"]')).not.toBeNull();
  });

  it('opens on the server TFA uses now, with its model, and says so', async () => {
    await create('Administrator', activeConfig('Gemini'));

    expect(serverSelect().value).toBe('Gemini');
    expect(modelSelect().value).toBe('Gemini|gemini-2.0-flash');
    expect(card('Gemini')).not.toBeNull();
    expect(card('Gemini').querySelector('[data-status="configured"]')!.textContent).toContain('In use');
    const using = el().querySelector('[data-using]')!.textContent!.replace(/\s+/g, ' ');
    expect(using).toContain('TFA uses gemini-2.0-flash on Google Gemini');
    expect(using).toContain('Only this model is offered in the TFA window');
  });

  it('has no separate enable switch: the chosen model and server is the one TFA uses', async () => {
    await create();

    expect(el().querySelector('[data-field="isEnabled"]')).toBeNull();
    choose('Gemini');
    expect(el().querySelector('[data-field="isEnabled"]')).toBeNull();
  });

  it('says what TFA will use once the choice is saved, and what it uses after', async () => {
    await create();
    const using = () => el().querySelector('[data-using]')!.textContent!.replace(/\s+/g, ' ');
    expect(using()).toContain('TFA uses llama3.1:8b on Ollama');

    pickModel('Gemini', 'gemini-2.5-flash');
    expect(using()).toContain('After you save, TFA will use gemini-2.5-flash on Google Gemini');

    click('[data-action="save-ai"]');
    http.expectOne(CONFIG_URL).flush(activeConfig('Gemini', { Gemini: { modelName: 'gemini-2.5-flash' } }));
    fixture.detectChanges();

    expect(using()).toContain('TFA uses gemini-2.5-flash on Google Gemini');
    expect(card('Gemini').querySelector('[data-status="configured"]')!.textContent).toContain('In use');
  });

  it('saves only the chosen server as the one in use, and switches the others off', async () => {
    await create();
    choose('OpenAI');
    enter(field('OpenAI', 'apiKey'), 'sk-proj-chosen-key-0001');

    click('[data-action="save-ai"]');

    const request = http.expectOne(CONFIG_URL);
    const enabled = request.request.body.providers.map((p: { kind: string; isEnabled: boolean }) => `${p.kind}:${p.isEnabled}`);
    expect(enabled).toEqual(['Ollama:false', 'Gemini:false', 'OpenAI:true']);
    request.flush(activeConfig('OpenAI', { OpenAI: { keySet: true, keyLast4: '0001', isConfigured: true } }));
  });

  it('says whether each server is in use, ready to be used, or still needs a key', async () => {
    await create();

    expect(card('Ollama').querySelector('[data-status="configured"]')!.textContent).toContain('In use');
    choose('Gemini');
    expect(card('Gemini').querySelector('[data-status="configured"]')!.textContent).toContain('Ready to use');
    choose('OpenAI');
    expect(card('OpenAI').querySelector('[data-status="configured"]')!.textContent).toContain('Needs an API key');
  });

  it('never shows a saved key: the box is empty and only "ends …1234" is said', async () => {
    await create();
    choose('Gemini');

    expect(field('Gemini', 'apiKey').value).toBe('');
    expect(field('Gemini', 'apiKey').type).toBe('password');
    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('ends …1234');
    expect(card('Gemini').querySelector('[data-hint="key"]')!.textContent).toContain('Leave the box empty to keep it');
    expect(el().innerHTML).not.toContain('sk-');
  });

  it('says so when a saved key can no longer be read, instead of saying a key is saved', async () => {
    await create('Administrator', config({ Gemini: { keySet: true, keyLast4: '1234', keyReadable: false, isConfigured: false } }));
    choose('Gemini');

    const hint = card('Gemini').querySelector('[data-hint="key"]')!.textContent!;
    expect(hint).toContain('can no longer be read');
    expect(hint).toContain('Enter it again');
    expect(hint).not.toContain('Leave the box empty to keep it');
    expect(card('Gemini').querySelector('[data-status="configured"]')!.textContent).toContain('Needs an API key');
  });

  it('keeps the stored key when the key box is left empty and only the model changes', async () => {
    await create();
    choose('Gemini');
    pickModel('Gemini', 'gemini-2.5-flash');

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
    choose('OpenAI');
    enter(field('OpenAI', 'apiKey'), 'sk-proj-brand-new-key-9999');

    click('[data-action="save-ai"]');
    const request = http.expectOne(CONFIG_URL);
    const openAi = request.request.body.providers.find((p: { kind: string }) => p.kind === 'OpenAI');
    expect(openAi.apiKey).toBe('sk-proj-brand-new-key-9999');
    expect(openAi.isEnabled).toBeTrue();
    request.flush(activeConfig('OpenAI', { OpenAI: { keySet: true, keyLast4: '9999', isConfigured: true } }));
    fixture.detectChanges();

    expect(field('OpenAI', 'apiKey').value).toBe('');
    expect(card('OpenAI').querySelector('[data-hint="key"]')!.textContent).toContain('ends …9999');
    expect(el().innerHTML).not.toContain('brand-new-key');
  });

  it('removes a stored key only when asked, and lets the user change their mind', async () => {
    await create();
    choose('Gemini');

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

  it('does not save invalid input: a bad address or a key with spaces', async () => {
    await create();
    enter(field('Ollama', 'baseUrl'), 'ftp://nope');

    click('[data-action="save-ai"]');

    http.expectNone(CONFIG_URL);
    expect(card('Ollama').querySelector('[data-error="baseUrl"]')).not.toBeNull();

    enter(field('Ollama', 'baseUrl'), 'http://localhost:11434');
    choose('Gemini');
    enter(field('Gemini', 'apiKey'), 'has a space');

    click('[data-action="save-ai"]');

    http.expectNone(CONFIG_URL);
    expect(card('Gemini').querySelector('[data-error="apiKey"]')).not.toBeNull();
  });

  it('shows a server error from saving', async () => {
    await create();
    choose('Gemini');
    pickModel('Gemini', 'gemini-2.5-flash');

    click('[data-action="save-ai"]');
    http.expectOne(CONFIG_URL).flush({ detail: 'Gemini does not accept that.' }, { status: 400, statusText: 'Bad Request' });
    fixture.detectChanges();

    expect(el().querySelector('[data-error="ai-save"]')!.textContent).toContain('Gemini does not accept that.');
  });

  describe('test connection', () => {
    function testResult(kind: string, result: AiProviderTestResult): void {
      choose(kind);
      click('[data-action="test-connection"]', card(kind));
      // A server that is not the one in use yet is saved first, which makes it the one in use.
      for (const save of http.match(CONFIG_URL)) {
        save.flush(activeConfig(kind as 'Ollama' | 'Gemini' | 'OpenAI', { OpenAI: { keySet: true } }));
        fixture.detectChanges();
      }
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

    it('is always clickable: before and after a test, while one is running, and with unsaved changes', async () => {
      await create();
      const button = () => card('Ollama').querySelector('[data-action="test-connection"]') as HTMLButtonElement;
      expect(button().disabled).toBeFalse();

      click('[data-action="test-connection"]', card('Ollama'));
      const first = http.expectOne('/api/v1/assistant/providers/Ollama/test');
      fixture.detectChanges();
      expect(button().textContent).toContain('Testing');
      expect(button().disabled).toBeFalse();

      // Clicking again while it runs starts over: the first test is dropped, the second one answers.
      click('[data-action="test-connection"]', card('Ollama'));
      expect(first.cancelled).toBeTrue();
      http.expectOne('/api/v1/assistant/providers/Ollama/test').flush({ ok: true, message: 'The connection works.', failureKind: null });
      fixture.detectChanges();
      expect(button().disabled).toBeFalse();
      expect(button().textContent).toContain('Test connection');

      // And once more after the result.
      click('[data-action="test-connection"]', card('Ollama'));
      http.expectOne('/api/v1/assistant/providers/Ollama/test').flush({ ok: false, message: 'Ollama is not running or cannot be reached.', failureKind: 'Unreachable' });
      fixture.detectChanges();
      expect(button().disabled).toBeFalse();
      expect(card('Ollama').querySelector('[data-result]')!.getAttribute('data-result')).toBe('Unreachable');
    });

    it('saves unsaved changes first and then tests the connection', async () => {
      await create();
      choose('Gemini');
      pickModel('Gemini', 'gemini-2.5-flash');
      const button = card('Gemini').querySelector('[data-action="test-connection"]') as HTMLButtonElement;
      expect(button.disabled).toBeFalse();
      expect(button.title).toContain('saved first');

      click('[data-action="test-connection"]', card('Gemini'));

      http.expectNone('/api/v1/assistant/providers/Gemini/test');
      const save = http.expectOne(CONFIG_URL);
      expect(save.request.method).toBe('PUT');
      expect(save.request.body.providers.find((p: { kind: string }) => p.kind === 'Gemini').modelName).toBe('gemini-2.5-flash');
      save.flush(activeConfig('Gemini', { Gemini: { modelName: 'gemini-2.5-flash' } }));
      fixture.detectChanges();

      http.expectOne('/api/v1/assistant/providers/Gemini/test').flush({ ok: true, message: 'The connection works.', failureKind: null });
      fixture.detectChanges();
      expect(card('Gemini').querySelector('[data-result]')!.getAttribute('data-result')).toBe('ok');
      expect(card('Gemini').querySelector('[data-action="test-connection"]')!.getAttribute('title')).toBe('');
    });

    it('does not test when the unsaved changes are not valid, and shows what is wrong', async () => {
      await create();
      enter(field('Ollama', 'baseUrl'), 'ftp://nope');

      click('[data-action="test-connection"]', card('Ollama'));

      http.expectNone(CONFIG_URL);
      http.expectNone('/api/v1/assistant/providers/Ollama/test');
      expect(card('Ollama').querySelector('[data-error="baseUrl"]')).not.toBeNull();
      expect((card('Ollama').querySelector('[data-action="test-connection"]') as HTMLButtonElement).disabled).toBeFalse();
    });

    it('does not test when saving the changes fails', async () => {
      await create();
      choose('Gemini');
      pickModel('Gemini', 'gemini-2.5-flash');

      click('[data-action="test-connection"]', card('Gemini'));
      http.expectOne(CONFIG_URL).flush({ detail: 'Gemini does not accept that.' }, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();

      http.expectNone('/api/v1/assistant/providers/Gemini/test');
      expect(el().querySelector('[data-error="ai-save"]')!.textContent).toContain('Gemini does not accept that.');
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
