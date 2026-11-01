import { Component, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, merge } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { AddAiModelRequest, AiApiType, AiLocality, AiProviderConfig, AiProviderTestResult } from '../../../assistant/models/assistant.model';
import { AiProviderAdminService } from '../services/ai-provider-admin.service';

export const DEFAULT_OLLAMA_URL = 'http://localhost:11434';

/** The example model ID shown in the model name box for each API type. */
export const MODEL_ID_EXAMPLES: Record<AiApiType, string> = {
  Gemini: 'gemini-3.8-flash',
  OpenAI: 'gpt-4o-mini',
  Ollama: 'llama3.2:3b'
};

/** The model ID rule for each API type, matching AiProviderCatalog on the server. Gemini IDs are lowercase letters,
 * digits, dots and hyphens, so a key's label such as "Gemini_TF_AI" is refused here; OpenAI-compatible and Ollama IDs
 * may hold uppercase, "_", ":" and "/" (fine-tuned IDs, Ollama tags, meta-llama/Llama-3.3-70B-Instruct), so only spaces
 * and "@" are refused (no model ID has an "@", so an email address the browser filled in is caught here). */
export const MODEL_ID_RULES: Record<AiApiType, RegExp> = {
  Gemini: /^[a-z0-9][a-z0-9.-]*$/,
  OpenAI: /^[^\s@]*$/,
  Ollama: /^[^\s@]*$/
};

export const MODEL_ID_MESSAGES: Record<AiApiType, string> = {
  Gemini:
    'A Google Gemini model name is its model ID: lowercase letters, digits, dots and hyphens, for example gemini-3.8-flash. ' +
    'The name you gave your key in Google AI Studio is not a model ID.',
  OpenAI: `A model name is the provider's model ID, without spaces or @, for example ${MODEL_ID_EXAMPLES.OpenAI}.`,
  Ollama: `A model name is the provider's model ID, without spaces or @, for example ${MODEL_ID_EXAMPLES.Ollama}.`
};

/** The API types a custom provider can use, as offered on the screen. */
export const CUSTOM_API_TYPES: { value: AiApiType; label: string }[] = [
  { value: 'OpenAI', label: 'OpenAI-compatible' },
  { value: 'Ollama', label: 'Ollama' }
];

const MIN_PROVIDER_NAME = 2;
const MAX_PROVIDER_NAME = 40;

/** The name as it is sent: trimmed, and for Gemini without the "models/" prefix Google AI Studio shows. */
export function normalizeModelName(apiType: AiApiType | '', value: string): string {
  const trimmed = value.trim();
  return apiType === 'Gemini' && trimmed.startsWith('models/') ? trimmed.slice('models/'.length) : trimmed;
}

/** The existing provider a typed name means, ignoring case and surrounding spaces; null for a new provider. */
export function matchProvider(providers: AiProviderConfig[], typed: string): AiProviderConfig | null {
  const name = typed.trim().toLowerCase();
  return name ? providers.find((provider) => provider.displayName.toLowerCase() === name) ?? null : null;
}

const SETTINGS_URL = '/settings/tfa';

type EditTarget = { providerId: number; name: string };

/** Add AI model, opened from TFA Settings, and its edit mode. The administrator types the AI provider: an existing one
 * (suggested while typing) shows its own key or address field; a new name is a custom provider and asks for its API type
 * (OpenAI-compatible or Ollama), server address, whether it runs locally or in the cloud, and an optional key. Test
 * connection tests the typed values without saving them; only when the test passes does the server save the model (and
 * create the custom provider), store the key and make the model the one TFA uses. On Edit the provider can be changed
 * too: the model then moves to that provider when the test works. A failed test saves nothing. Needs settings.ai.edit and
 * settings.ai.test. */
@Component({
  selector: 'app-add-ai-model',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './add-ai-model.component.html',
  styleUrl: './add-ai-model.component.css'
})
export class AddAiModelComponent implements OnInit, OnDestroy {
  private readonly admin = inject(AiProviderAdminService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = viewChild(ConfirmDialogComponent);

  /** Edit mode (settings/tfa/models/:providerId/:name/edit): the model is renamed or moved, not added. */
  readonly editing: EditTarget | null = this.editTarget();
  readonly removingKey = signal(false);

  readonly settingsUrl = SETTINGS_URL;
  readonly defaultOllamaUrl = DEFAULT_OLLAMA_URL;
  readonly modelExamples = MODEL_ID_EXAMPLES;
  readonly modelIdMessages = MODEL_ID_MESSAGES;
  readonly customApiTypes = CUSTOM_API_TYPES;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly providers = signal<AiProviderConfig[]>([]);
  readonly testing = signal(false);
  readonly result = signal<AiProviderTestResult | null>(null);
  readonly requestError = signal<string | null>(null);

  readonly form = new FormGroup(
    {
      provider: new FormControl('', { nonNullable: true, validators: [notBlank, Validators.maxLength(MAX_PROVIDER_NAME)] }),
      modelName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100), notBlank, (control) => this.modelIdCheck(control)] }),
      apiKey: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\S*$/), Validators.maxLength(500)] }),
      baseUrl: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^(https?:\/\/\S+)?$/i), Validators.maxLength(300)] }),
      apiType: new FormControl<AiApiType>('OpenAI', { nonNullable: true }),
      locality: new FormControl<AiLocality>('Cloud', { nonNullable: true })
    },
    { validators: [(group) => this.keyRequired(group), (group) => this.newProviderChecks(group)] }
  );

  private readonly typedProvider = toSignal(this.form.controls.provider.valueChanges, { initialValue: this.form.controls.provider.value });
  private readonly typedApiType = toSignal(this.form.controls.apiType.valueChanges, { initialValue: this.form.controls.apiType.value });

  /** The existing provider the typed name means, with what the server says about its saved key and address. */
  readonly selected = computed(() => matchProvider(this.providers(), this.typedProvider()));

  /** A name that matches no provider: the screen asks for the new provider's details. */
  readonly isNew = computed(() => this.typedProvider().trim().length > 0 && this.selected() === null);

  /** How the typed model ID is checked: by the matched provider's API type, or the one chosen for a new provider. */
  readonly apiType = computed<AiApiType | ''>(() => this.selected()?.apiType ?? (this.isNew() ? this.typedApiType() : ''));

  /** Edit mode: the provider the model is on now. */
  readonly source = computed(() => (this.editing ? this.providers().find((provider) => provider.providerId === this.editing!.providerId) ?? null : null));

  /** Edit mode: where the model goes when the test works, when that is not where it is now. */
  readonly moveTarget = computed(() => {
    const source = this.source();
    if (!source || !this.typedProvider().trim()) {
      return null;
    }

    const target = this.selected();
    if (target?.providerId === source.providerId) {
      return null;
    }

    return target?.displayName ?? this.typedProvider().trim();
  });

  private run: Subscription | null = null;

  constructor() {
    // The model ID rule depends on the provider, so check the typed name again when the provider or API type changes.
    merge(this.form.controls.provider.valueChanges, this.form.controls.apiType.valueChanges)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.form.controls.modelName.updateValueAndValidity();
        this.form.updateValueAndValidity();
      });
  }

  ngOnInit(): void {
    if (!this.auth.hasPermission(Permissions.SettingsAiEdit) || !this.auth.hasPermission(Permissions.SettingsAiTest)) {
      void this.router.navigateByUrl(SETTINGS_URL);
      return;
    }

    this.load();
  }

  ngOnDestroy(): void {
    this.run?.unsubscribe();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.admin.getConfig().subscribe({
      next: (config) => {
        this.providers.set(config);
        this.loading.set(false);
        this.form.updateValueAndValidity();
        if (this.editing) {
          this.startEditing(config, this.editing);
        }
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the AI providers.'));
        this.loading.set(false);
      }
    });
  }

  /** True when the provider has a saved key that can still be read, so the key box may be left empty. */
  hasUsableKey(provider: AiProviderConfig | null): boolean {
    return !!provider && provider.keySet && provider.keyReadable;
  }

  /** An existing provider that is called at a stored address: Ollama and custom providers. */
  usesAddress(provider: AiProviderConfig): boolean {
    return provider.apiType === 'Ollama' || !provider.isBuiltIn;
  }

  /** An existing provider that takes a key: every one except an Ollama one. */
  takesKey(provider: AiProviderConfig): boolean {
    return provider.apiType !== 'Ollama';
  }

  /** What the key box says under it. */
  keyHint(provider: AiProviderConfig): string {
    if (this.hasUsableKey(provider)) {
      const ends = provider.keyLast4 ? ` (ends …${provider.keyLast4})` : '';
      return `A key is saved${ends}. Leave the box empty to keep it. A new key replaces it for every ${provider.displayName} model.`;
    }

    if (provider.keySet && !provider.keyReadable) {
      return 'The saved key can no longer be read. Enter it again.';
    }

    return provider.requiresKey
      ? `Paste the ${provider.displayName} API key. It is saved for every ${provider.displayName} model.`
      : `Optional: a key for ${provider.displayName}, if its server needs one.`;
  }

  /** The page is changed: an old result no longer describes what is typed. */
  clearResult(): void {
    this.result.set(null);
    this.requestError.set(null);
  }

  /** Tests the typed values. Success: the model is saved (on Edit: renamed or moved), so go back to TFA Settings.
   * Failure: show why, stay here with the values kept, and empty the key box so a rejected key is not left on screen. */
  test(): void {
    this.form.markAllAsTouched();
    const target = this.selected();
    const apiType = this.apiType();
    if (this.testing() || this.form.invalid || !apiType) {
      return;
    }

    const value = this.form.getRawValue();
    const modelName = normalizeModelName(apiType, value.modelName);
    const key = value.apiKey.trim();
    const body: AddAiModelRequest = { modelName };
    if (key) {
      body.apiKey = key;
    }

    if (target) {
      body.providerId = target.providerId;
      if (this.usesAddress(target)) {
        body.baseUrl = value.baseUrl.trim() || null;
      }
    } else {
      body.newProvider = { name: value.provider.trim(), apiType, baseUrl: value.baseUrl.trim(), locality: value.locality };
    }

    const providerName = target?.displayName ?? value.provider.trim();
    this.clearResult();
    this.testing.set(true);
    const editing = this.editing;
    const request = editing ? this.admin.editModel(editing.providerId, editing.name, body) : this.admin.addModel(body);
    this.run = request.subscribe({
      next: (result) => {
        this.testing.set(false);
        if (result.ok) {
          this.toast.success(editing ? `${modelName} was updated` : `${modelName} on ${providerName} is now in use`);
          void this.router.navigateByUrl(SETTINGS_URL);
          return;
        }

        this.result.set(result);
        this.form.controls.apiKey.reset('');
      },
      error: (err: unknown) => {
        this.testing.set(false);
        this.requestError.set(apiErrorMessage(err, 'The test could not be run.'));
        this.form.controls.apiKey.reset('');
      }
    });
  }

  /** Edit mode: removes the model's provider's key after confirmation, without a test (nothing is sent to the provider).
   * Every model of that provider is then without a key. */
  async removeKey(): Promise<void> {
    const provider = this.source();
    const editing = this.editing;
    if (!provider || !editing || this.removingKey()) {
      return;
    }

    const inUse = provider.isEnabled
      ? ` TFA uses ${provider.displayName}, so TFA is unavailable until a key is added again.`
      : '';
    const confirmed = await this.dialog()?.open({
      title: `Remove the ${provider.displayName} key?`,
      message: `The key is removed for every ${provider.displayName} model.${inUse}`,
      confirmLabel: 'Remove the key',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.clearResult();
    this.removingKey.set(true);
    this.admin.editModel(provider.providerId, editing.name, { modelName: editing.name, clearKey: true }).subscribe({
      next: () => {
        this.removingKey.set(false);
        this.toast.success(`The ${provider.displayName} key was removed`);
        void this.router.navigateByUrl(SETTINGS_URL);
      },
      error: (err: unknown) => {
        this.removingKey.set(false);
        this.requestError.set(apiErrorMessage(err, 'Could not remove the key.'));
      }
    });
  }

  private editTarget(): EditTarget | null {
    const params = this.route.snapshot.paramMap;
    const providerId = Number(params.get('providerId'));
    const name = params.get('name');
    return Number.isInteger(providerId) && providerId > 0 && name ? { providerId, name } : null;
  }

  /** Fills the form for the model being edited. A model that cannot be edited here (unknown, built-in, or the one TFA
   * uses) sends the administrator back with the reason. */
  private startEditing(config: AiProviderConfig[], target: EditTarget): void {
    const provider = config.find((candidate) => candidate.providerId === target.providerId);
    let refusal: string | null = null;
    if (!provider || !(provider.models ?? []).includes(target.name)) {
      refusal = 'That model is not in the list.';
    } else if (!(provider.customModels ?? []).includes(target.name)) {
      refusal = 'A built-in model cannot be changed or removed.';
    } else if (provider.isEnabled && provider.modelName === target.name) {
      refusal = `${target.name} is in use by TFA. Switch TFA to another model first, then edit it.`;
    }

    if (refusal || !provider) {
      this.toast.error(refusal ?? 'That model is not in the list.');
      void this.router.navigateByUrl(SETTINGS_URL);
      return;
    }

    this.form.controls.provider.setValue(provider.displayName);
    this.form.controls.modelName.setValue(target.name);
  }

  /** A model name is the provider's model ID, in the format of the provider's API type (the server checks the same).
   * Before a provider is typed only spaces are refused. Blank names are reported by their own rule. */
  private modelIdCheck(control: AbstractControl): ValidationErrors | null {
    const value = String(control.value ?? '');
    if (!value.trim()) {
      return null;
    }

    const typed = String(control.parent?.get('provider')?.value ?? '');
    const matched = matchProvider(this.providers?.() ?? [], typed);
    const chosen = (control.parent?.get('apiType')?.value as AiApiType | undefined) ?? 'OpenAI';
    const apiType: AiApiType | '' = matched?.apiType ?? (typed.trim() ? chosen : '');
    const rule = apiType ? MODEL_ID_RULES[apiType] : /^[^\s@]*$/;
    return rule.test(normalizeModelName(apiType, value)) ? null : { modelId: true };
  }

  /** A built-in cloud provider without a readable saved key needs one typed. */
  private keyRequired(group: AbstractControl): ValidationErrors | null {
    const provider = matchProvider(this.providers?.() ?? [], String(group.get('provider')?.value ?? ''));
    if (!provider?.requiresKey || this.hasUsableKey(provider)) {
      return null;
    }

    return String(group.get('apiKey')?.value ?? '').trim() ? null : { apiKeyRequired: true };
  }

  /** A new provider needs a name of 2 to 40 characters and a server address. */
  private newProviderChecks(group: AbstractControl): ValidationErrors | null {
    const typed = String(group.get('provider')?.value ?? '').trim();
    if (!typed || matchProvider(this.providers?.() ?? [], typed)) {
      return null;
    }

    const errors: ValidationErrors = {};
    if (typed.length < MIN_PROVIDER_NAME) {
      errors['providerNameLength'] = true;
    }
    if (!String(group.get('baseUrl')?.value ?? '').trim()) {
      errors['baseUrlRequired'] = true;
    }

    return Object.keys(errors).length ? errors : null;
  }
}

function notBlank(control: AbstractControl): ValidationErrors | null {
  return String(control.value ?? '').trim() ? null : { required: true };
}
