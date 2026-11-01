import { Component, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { AddAiModelRequest, AiProviderConfig, AiProviderKind, AiProviderTestResult } from '../../../assistant/models/assistant.model';
import { AiProviderAdminService } from '../services/ai-provider-admin.service';

export const DEFAULT_OLLAMA_URL = 'http://localhost:11434';

/** The example model ID shown in the model name box for each provider. */
export const MODEL_ID_EXAMPLES: Record<AiProviderKind, string> = {
  Gemini: 'gemini-3.8-flash',
  OpenAI: 'gpt-4o-mini',
  Ollama: 'llama3.2:3b'
};

/** The model ID rule for each provider, matching AiProviderCatalog on the server. Gemini IDs are lowercase letters,
 * digits, dots and hyphens, so a key's label such as "Gemini_TF_AI" is refused here; OpenAI and Ollama IDs may hold
 * uppercase, "_", ":" and "/" (fine-tuned IDs, Ollama tags), so only spaces are refused. */
export const MODEL_ID_RULES: Record<AiProviderKind, RegExp> = {
  Gemini: /^[a-z0-9][a-z0-9.-]*$/,
  OpenAI: /^\S*$/,
  Ollama: /^\S*$/
};

export const MODEL_ID_MESSAGES: Record<AiProviderKind, string> = {
  Gemini:
    'A Google Gemini model name is its model ID: lowercase letters, digits, dots and hyphens, for example gemini-3.8-flash. ' +
    'The name you gave your key in Google AI Studio is not a model ID.',
  OpenAI: `A model name is the provider's model ID, without spaces, for example ${MODEL_ID_EXAMPLES.OpenAI}.`,
  Ollama: `A model name is the provider's model ID, without spaces, for example ${MODEL_ID_EXAMPLES.Ollama}.`
};

/** The name as it is sent: trimmed, and for Gemini without the "models/" prefix Google AI Studio shows. */
export function normalizeModelName(kind: AiProviderKind | '', value: string): string {
  const trimmed = value.trim();
  return kind === 'Gemini' && trimmed.startsWith('models/') ? trimmed.slice('models/'.length) : trimmed;
}

const SETTINGS_URL = '/settings/tfa';

/** Add AI model, opened from TFA Settings: the administrator picks the provider, types the model name and either the
 * API key (Gemini, OpenAI) or the server address (Ollama), and clicks Test connection. The server tests those values
 * without saving them; only when the test passes does it save the model, store the key and make it the one TFA uses,
 * and this page goes back to TFA Settings. A failed test saves nothing. Needs settings.ai.edit and settings.ai.test. */
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

  /** Edit mode (settings/tfa/models/:kind/:name/edit): the provider is fixed and the model is renamed, not added. */
  readonly editing: { kind: AiProviderKind; name: string } | null = this.editTarget();
  readonly removingKey = signal(false);

  readonly settingsUrl = SETTINGS_URL;
  readonly defaultOllamaUrl = DEFAULT_OLLAMA_URL;
  readonly modelExamples = MODEL_ID_EXAMPLES;
  readonly modelIdMessages = MODEL_ID_MESSAGES;

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly providers = signal<AiProviderConfig[]>([]);
  readonly testing = signal(false);
  readonly result = signal<AiProviderTestResult | null>(null);
  readonly requestError = signal<string | null>(null);

  readonly form = new FormGroup(
    {
      kind: new FormControl<AiProviderKind | ''>('', { nonNullable: true, validators: [Validators.required] }),
      modelName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100), notBlank, modelId] }),
      apiKey: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\S*$/), Validators.maxLength(500)] }),
      baseUrl: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^(https?:\/\/\S+)?$/i), Validators.maxLength(300)] })
    },
    { validators: (group) => this.keyRequired(group) }
  );

  private readonly kind = toSignal(this.form.controls.kind.valueChanges, { initialValue: this.form.controls.kind.value });

  /** The provider chosen in the first field, with what the server says about its saved key and address. */
  readonly selected = computed(() => this.providers().find((provider) => provider.kind === this.kind()) ?? null);

  private run: Subscription | null = null;

  constructor() {
    // The model ID rule depends on the provider, so check the typed name again when the provider changes.
    this.form.controls.kind.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.modelName.updateValueAndValidity());
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

  /** True when the chosen provider has a saved key that can still be read, so the key box may be left empty. */
  hasUsableKey(provider: AiProviderConfig | null): boolean {
    return !!provider && provider.requiresKey && provider.keySet && provider.keyReadable;
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

    return `Paste the ${provider.displayName} API key. It is saved for every ${provider.displayName} model.`;
  }

  /** The page is changed: an old result no longer describes what is typed. */
  clearResult(): void {
    this.result.set(null);
    this.requestError.set(null);
  }

  /** Tests the typed values. Success: the model is saved and in use, so go back to TFA Settings. Failure: show why, stay
   * here with the values kept, and empty the key box so a rejected key is not left on screen. */
  test(): void {
    this.form.markAllAsTouched();
    const provider = this.selected();
    if (this.testing() || this.form.invalid || !provider) {
      return;
    }

    const value = this.form.getRawValue();
    const modelName = normalizeModelName(provider.kind, value.modelName);
    const body: AddAiModelRequest = { modelName };
    if (provider.requiresKey) {
      const key = value.apiKey.trim();
      if (key) {
        body.apiKey = key;
      }
    } else {
      body.baseUrl = value.baseUrl.trim() || null;
    }

    this.clearResult();
    this.testing.set(true);
    const editing = this.editing;
    const request = editing ? this.admin.editModel(provider.kind, editing.name, body) : this.admin.addModel(provider.kind, body);
    this.run = request.subscribe({
      next: (result) => {
        this.testing.set(false);
        if (result.ok) {
          this.toast.success(editing ? `${modelName} was updated` : `${modelName} on ${provider.displayName} is now in use`);
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

  /** Edit mode: removes the provider's key after confirmation, without a test (nothing is sent to the provider). Every
   * model of that provider is then without a key. */
  async removeKey(): Promise<void> {
    const provider = this.selected();
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
    this.admin.editModel(provider.kind, editing.name, { modelName: editing.name, clearKey: true }).subscribe({
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

  private editTarget(): { kind: AiProviderKind; name: string } | null {
    const params = this.route.snapshot.paramMap;
    const kind = params.get('kind') as AiProviderKind | null;
    const name = params.get('name');
    return kind && name ? { kind, name } : null;
  }

  /** Fills the form for the model being edited. A model that cannot be edited here (unknown, built-in, or the one TFA
   * uses) sends the administrator back with the reason. */
  private startEditing(config: AiProviderConfig[], target: { kind: AiProviderKind; name: string }): void {
    const provider = config.find((candidate) => candidate.kind === target.kind);
    let refusal: string | null = null;
    if (!provider || !(provider.models ?? []).includes(target.name)) {
      refusal = 'That model is not in the list.';
    } else if (!(provider.customModels ?? []).includes(target.name)) {
      refusal = 'A built-in model cannot be changed or removed.';
    } else if (provider.isEnabled && provider.modelName === target.name) {
      refusal = `${target.name} is in use by TFA. Switch TFA to another model first, then edit it.`;
    }

    if (refusal) {
      this.toast.error(refusal);
      void this.router.navigateByUrl(SETTINGS_URL);
      return;
    }

    this.form.controls.kind.setValue(target.kind);
    this.form.controls.kind.disable();
    this.form.controls.modelName.setValue(target.name);
  }

  /** A cloud provider without a readable saved key needs one typed. */
  private keyRequired(group: AbstractControl): ValidationErrors | null {
    const kind = group.get('kind')?.value as AiProviderKind | '';
    const provider = this.providers?.().find((candidate) => candidate.kind === kind) ?? null;
    if (!provider?.requiresKey || this.hasUsableKey(provider)) {
      return null;
    }

    return String(group.get('apiKey')?.value ?? '').trim() ? null : { apiKeyRequired: true };
  }
}

function notBlank(control: AbstractControl): ValidationErrors | null {
  return String(control.value ?? '').trim() ? null : { required: true };
}

/** A model name is the provider's model ID, in the format of the provider chosen above (the server checks the same).
 * Before a provider is chosen only spaces are refused. Blank names are reported by their own rule. */
function modelId(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');
  if (!value.trim()) {
    return null;
  }

  const kind = (control.parent?.get('kind')?.value ?? '') as AiProviderKind | '';
  const rule = kind ? MODEL_ID_RULES[kind] : /^\S*$/;
  return rule.test(normalizeModelName(kind, value)) ? null : { modelId: true };
}
