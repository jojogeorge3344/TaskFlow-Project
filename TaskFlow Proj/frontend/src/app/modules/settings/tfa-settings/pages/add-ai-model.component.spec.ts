import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { ToastService } from '../../../../shared/services/toast.service';
import { ADMINISTRATOR_PERMISSIONS, fakeAuthService } from '../../../../shared/testing/permissions';
import { AiProviderConfig } from '../../../assistant/models/assistant.model';
import { AddAiModelComponent } from './add-ai-model.component';

const CONFIG_URL = '/api/v1/assistant/providers/config';

function config(): AiProviderConfig[] {
  return [
    {
      kind: 'Ollama', displayName: 'Ollama', locality: 'Local', requiresKey: false, isEnabled: true, modelName: 'llama3.2:3b',
      baseUrl: 'http://localhost:11434', keySet: false, keyLast4: null, isConfigured: true, keyReadable: true,
      models: ['llama3.2:3b'], customModels: []
    },
    {
      kind: 'Gemini', displayName: 'Google Gemini', locality: 'Cloud', requiresKey: true, isEnabled: false, modelName: 'gemini-2.0-flash',
      baseUrl: null, keySet: true, keyLast4: '1234', isConfigured: false, keyReadable: true,
      models: ['gemini-2.0-flash'], customModels: []
    },
    {
      kind: 'OpenAI', displayName: 'OpenAI GPT', locality: 'Cloud', requiresKey: true, isEnabled: false, modelName: 'gpt-4o-mini',
      baseUrl: null, keySet: false, keyLast4: null, isConfigured: false, keyReadable: true,
      models: ['gpt-4o-mini'], customModels: []
    }
  ];
}

describe('AddAiModelComponent', () => {
  let fixture: ComponentFixture<AddAiModelComponent>;
  let http: HttpTestingController;
  let router: Router;
  let toast: jasmine.SpyObj<ToastService>;
  let permissions: string[];

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function field<T extends HTMLElement>(name: string): T {
    return el().querySelector(`[data-field="${name}"]`) as T;
  }

  function choose(kind: string): void {
    const select = field<HTMLSelectElement>('kind');
    select.value = kind;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function type(name: string, value: string): void {
    const input = field<HTMLInputElement>(name);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function clickTest(): void {
    (el().querySelector('[data-action="test-connection"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function open(): void {
    fixture = TestBed.createComponent(AddAiModelComponent);
    fixture.detectChanges();
  }

  function openLoaded(): void {
    open();
    http.expectOne(CONFIG_URL).flush(config());
    fixture.detectChanges();
  }

  beforeEach(() => {
    permissions = [...ADMINISTRATOR_PERMISSIONS];
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      imports: [AddAiModelComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: fakeAuthService(() => ({ permissions })) },
        { provide: ToastService, useValue: toast }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
  });

  afterEach(() => http.verify());

  it('asks for the provider first and shows no other field until one is chosen', () => {
    openLoaded();

    expect(field('kind')).toBeTruthy();
    expect(field('modelName')).toBeNull();
    expect(field('apiKey')).toBeNull();
    expect(field('baseUrl')).toBeNull();
  });

  it('shows the API key for a cloud provider and the optional server address for Ollama', () => {
    openLoaded();

    choose('OpenAI');
    expect(field('modelName')).toBeTruthy();
    expect(field('apiKey')).toBeTruthy();
    expect(field('baseUrl')).toBeNull();

    choose('Ollama');
    expect(field('apiKey')).toBeNull();
    expect(field('baseUrl')).toBeTruthy();
    expect(el().textContent).toContain('(optional)');
  });

  it('needs the model name, and a key for a provider with none saved, before sending anything', () => {
    openLoaded();

    clickTest();
    expect(el().querySelector('[data-error="kind"]')).toBeTruthy();

    choose('OpenAI');
    clickTest();
    expect(el().querySelector('[data-error="modelName"]')).toBeTruthy();
    expect(el().querySelector('[data-error="apiKey"]')?.textContent).toContain('Enter the API key');
    http.expectNone((request) => request.url.endsWith('/models'));
  });

  it('refuses a model name with spaces, such as a typed label, before sending anything', () => {
    openLoaded();
    choose('OpenAI');

    type('modelName', 'GPT API Key');
    type('apiKey', 'sk-new-key-9999');
    clickTest();

    expect(el().querySelector('[data-error="modelName"]')?.textContent).toContain(
      "A model name is the provider's model ID, without spaces, for example gpt-4o-mini."
    );
    http.expectNone((request) => request.url.endsWith('/models'));
  });

  it('refuses a Gemini key label typed as the model, before sending anything', () => {
    openLoaded();
    choose('Gemini');

    type('modelName', 'Gemini_TF_AI');
    clickTest();

    const error = el().querySelector('[data-error="modelName"]')?.textContent ?? '';
    expect(error).toContain('lowercase letters, digits, dots and hyphens, for example gemini-3.8-flash.');
    expect(error).toContain('The name you gave your key in Google AI Studio is not a model ID.');
    http.expectNone((request) => request.url.endsWith('/models'));
  });

  it('sends a Gemini name copied with the models/ prefix without it', () => {
    openLoaded();
    choose('Gemini');

    type('modelName', 'models/gemini-3.8-flash');
    clickTest();

    const request = http.expectOne('/api/v1/assistant/providers/Gemini/models');
    expect(request.request.body).toEqual({ modelName: 'gemini-3.8-flash' });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null, providers: config() });
  });

  it('accepts an Ollama tag, and checks the name again when the provider changes', () => {
    openLoaded();
    choose('Ollama');
    type('modelName', 'Llama3.2:3b');
    expect(fixture.componentInstance.form.controls.modelName.valid).toBeTrue();

    choose('Gemini');
    expect(fixture.componentInstance.form.controls.modelName.hasError('modelId')).toBeTrue();
    clickTest();

    expect(el().querySelector('[data-error="modelName"]')?.textContent).toContain('Google AI Studio');
    http.expectNone((request) => request.url.endsWith('/models'));
  });

  it('shows a malformed model name reported by the provider, and that nothing was saved', () => {
    openLoaded();
    choose('Gemini');
    type('modelName', 'gemini-3.8-flash');
    clickTest();

    http.expectOne('/api/v1/assistant/providers/Gemini/models').flush({
      ok: false,
      message: 'Google Gemini did not accept the configured model name as a model ID.',
      failureKind: 'InvalidModelName',
      providers: null
    });
    fixture.detectChanges();

    const shown = el().querySelector('[data-result="InvalidModelName"]')?.textContent ?? '';
    expect(shown).toContain('did not accept the configured model name as a model ID.');
    expect(shown).toContain('Nothing was saved');
  });

  it('shows an example model ID for each provider, and where to get a Gemini key', () => {
    openLoaded();

    const examples: Record<string, string> = { Gemini: 'gemini-3.8-flash', OpenAI: 'gpt-4o-mini', Ollama: 'llama3.2:3b' };
    for (const [kind, example] of Object.entries(examples)) {
      choose(kind);
      expect(field<HTMLInputElement>('modelName').placeholder).toBe(`For example ${example}`);
      expect(!!el().querySelector('[data-hint="key-source"]')).toBe(kind === 'Gemini');
    }

    choose('Gemini');
    const link = el().querySelector('[data-hint="key-source"] a') as HTMLAnchorElement;
    expect(link.href).toBe('https://aistudio.google.com/apikey');
    expect(link.rel).toContain('noopener');
  });

  it('lets the key be left empty when the provider already has a readable key', () => {
    openLoaded();
    choose('Gemini');

    expect(el().querySelector('[data-hint="key"]')?.textContent).toContain('ends …1234');
    type('modelName', 'gemini-2.5-pro');
    clickTest();

    const request = http.expectOne('/api/v1/assistant/providers/Gemini/models');
    expect(request.request.body).toEqual({ modelName: 'gemini-2.5-pro' });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null, providers: config() });
  });

  it('goes back to TFA settings with the model in use when the test passes', () => {
    openLoaded();
    choose('OpenAI');
    type('modelName', ' gpt-4.1-mini ');
    type('apiKey', 'sk-new-key-9999');
    clickTest();

    const request = http.expectOne('/api/v1/assistant/providers/OpenAI/models');
    expect(request.request.body).toEqual({ modelName: 'gpt-4.1-mini', apiKey: 'sk-new-key-9999' });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null, providers: config() });

    expect(toast.success).toHaveBeenCalledWith('gpt-4.1-mini on OpenAI GPT is now in use');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/settings/tfa');
  });

  it('stays on the page with the reason and an empty key box when the test fails', () => {
    openLoaded();
    choose('OpenAI');
    type('modelName', 'gpt-typo');
    type('apiKey', 'sk-wrong');
    clickTest();

    http.expectOne('/api/v1/assistant/providers/OpenAI/models').flush({
      ok: false, message: 'OpenAI GPT did not accept the API key.', failureKind: 'InvalidKey', providers: null
    });
    fixture.detectChanges();

    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(el().querySelector('[data-result="InvalidKey"]')?.textContent).toContain('Nothing was saved');
    expect(field<HTMLInputElement>('apiKey').value).toBe('');
    expect(field<HTMLInputElement>('modelName').value).toBe('gpt-typo');
  });

  it('sends Ollama with no key and an empty address as null', () => {
    openLoaded();
    choose('Ollama');
    type('modelName', 'phi3:mini');
    clickTest();

    const request = http.expectOne('/api/v1/assistant/providers/Ollama/models');
    expect(request.request.body).toEqual({ modelName: 'phi3:mini', baseUrl: null });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null, providers: config() });
  });

  it('has a Back link to TFA settings that sends nothing', () => {
    openLoaded();

    const back = el().querySelector('[data-action="back"]') as HTMLAnchorElement;
    expect(back.getAttribute('href')).toBe('/settings/tfa');
  });

  it('sends a user without the test permission back to TFA settings', () => {
    permissions = permissions.filter((key) => key !== Permissions.SettingsAiTest);
    open();

    http.expectNone(CONFIG_URL);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/settings/tfa');
  });
});

/** Gemini has the added models gemini-2.5-pro (in use) and gemini-2.0-flash, and the built-in gemini-3.8-flash. */
function editConfig(): AiProviderConfig[] {
  return config().map((provider) =>
    provider.kind === 'Gemini'
      ? {
          ...provider,
          isEnabled: true,
          isConfigured: true,
          modelName: 'gemini-2.5-pro',
          models: ['gemini-3.8-flash', 'gemini-2.5-pro', 'gemini-2.0-flash'],
          customModels: ['gemini-2.5-pro', 'gemini-2.0-flash']
        }
      : { ...provider, isEnabled: false }
  );
}

describe('AddAiModelComponent in edit mode', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let router: Router;
  let toast: jasmine.SpyObj<ToastService>;

  function el(): HTMLElement {
    return harness.routeNativeElement as HTMLElement;
  }

  function field<T extends HTMLElement>(name: string): T {
    return el().querySelector(`[data-field="${name}"]`) as T;
  }

  function type(name: string, value: string): void {
    const input = field<HTMLInputElement>(name);
    input.value = value;
    input.dispatchEvent(new Event('input'));
    harness.detectChanges();
  }

  function clickTest(): void {
    (el().querySelector('[data-action="test-connection"]') as HTMLButtonElement).click();
    harness.detectChanges();
  }

  async function openEdit(kind: string, name: string, load: AiProviderConfig[] = editConfig()): Promise<void> {
    await harness.navigateByUrl(`/settings/tfa/models/${kind}/${name}/edit`);
    http.expectOne(CONFIG_URL).flush(load);
    harness.detectChanges();
    await harness.fixture.whenStable();
  }

  beforeEach(async () => {
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'settings/tfa/models/:kind/:name/edit', component: AddAiModelComponent },
          { path: 'settings/tfa', children: [] }
        ]),
        { provide: AuthService, useValue: fakeAuthService(() => ({ permissions: [...ADMINISTRATOR_PERMISSIONS] })) },
        { provide: ToastService, useValue: toast }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => {
    const dialog = (harness.fixture.nativeElement as HTMLElement).querySelector('dialog');
    if (dialog?.open) {
      dialog.close();
    }
    http.verify();
  });

  it('fixes the provider, fills in the model name and says the key is shared', async () => {
    await openEdit('Gemini', 'gemini-2.0-flash');

    expect(el().querySelector('h1')?.textContent).toContain('Edit AI model');
    expect(field<HTMLSelectElement>('kind').disabled).toBeTrue();
    expect(field<HTMLSelectElement>('kind').value).toBe('Gemini');
    expect(field<HTMLInputElement>('modelName').value).toBe('gemini-2.0-flash');
    expect(el().querySelector('[data-hint="key"]')?.textContent).toContain(
      'A key is saved (ends …1234). Leave the box empty to keep it. A new key replaces it for every Google Gemini model.'
    );
    expect(el().querySelector('[data-action="test-connection"]')?.textContent).toContain('Test and save');
  });

  it('saves a rename through the edit endpoint and goes back with a toast', async () => {
    await openEdit('Gemini', 'gemini-2.0-flash');
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    type('modelName', 'gemini-2.5-flash');
    clickTest();

    const request = http.expectOne((candidate) => candidate.method === 'PUT' && candidate.url === '/api/v1/assistant/providers/Gemini/models');
    expect(request.request.params.get('name')).toBe('gemini-2.0-flash');
    expect(request.request.body).toEqual({ modelName: 'gemini-2.5-flash' });
    request.flush({ ok: true, message: 'The connection works.', failureKind: null, providers: editConfig() });

    expect(toast.success).toHaveBeenCalledWith('gemini-2.5-flash was updated');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/settings/tfa');
  });

  it('stays with the reason and an empty key box when the test fails', async () => {
    await openEdit('Gemini', 'gemini-2.0-flash');

    type('apiKey', 'AIza-wrong-key');
    clickTest();
    http.expectOne((candidate) => candidate.method === 'PUT').flush({
      ok: false, message: 'Google Gemini did not accept the API key.', failureKind: 'InvalidKey', providers: null
    });
    harness.detectChanges();

    expect(el().querySelector('[data-result="InvalidKey"]')?.textContent).toContain('Nothing was saved');
    expect(field<HTMLInputElement>('apiKey').value).toBe('');
    expect(field<HTMLInputElement>('modelName').value).toBe('gemini-2.0-flash');
  });

  it('sends a built-in model back to TFA settings with the reason', async () => {
    await openEdit('Gemini', 'gemini-3.8-flash');

    expect(toast.error).toHaveBeenCalledWith('A built-in model cannot be changed or removed.');
    expect(router.url).toBe('/settings/tfa');
  });

  it('sends the model in use back to TFA settings with the reason', async () => {
    await openEdit('Gemini', 'gemini-2.5-pro');

    expect(toast.error).toHaveBeenCalledWith('gemini-2.5-pro is in use by TFA. Switch TFA to another model first, then edit it.');
    expect(router.url).toBe('/settings/tfa');
  });

  it('removes the key after a confirmation that warns when TFA uses the provider', async () => {
    await openEdit('Gemini', 'gemini-2.0-flash');
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    (el().querySelector('[data-action="remove-key"]') as HTMLButtonElement).click();
    harness.detectChanges();
    const dialog = el().querySelector('dialog')!;
    expect(dialog.textContent).toContain('The key is removed for every Google Gemini model.');
    expect(dialog.textContent).toContain('TFA uses Google Gemini, so TFA is unavailable until a key is added again.');
    (dialog.querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    await harness.fixture.whenStable();

    const request = http.expectOne((candidate) => candidate.method === 'PUT');
    expect(request.request.body).toEqual({ modelName: 'gemini-2.0-flash', clearKey: true });
    request.flush({ ok: true, message: 'The key was removed.', failureKind: null, providers: editConfig() });

    expect(toast.success).toHaveBeenCalledWith('The Google Gemini key was removed');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/settings/tfa');
  });
});
