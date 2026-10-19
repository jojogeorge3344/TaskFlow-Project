import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { AiProviderConfig, AiProviderKind, AiProviderTestResult, UpdateAiProvider } from '../../core/models';
import { AiProviderAdminService } from '../../core/services/ai-provider-admin.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';

type ProviderForm = FormGroup<{
  isEnabled: FormControl<boolean>;
  modelName: FormControl<string>;
  baseUrl: FormControl<string>;
  apiKey: FormControl<string>;
}>;

/** The "AI assistant" section of Application Settings: which providers TFA may use, their model, the Ollama address and
 * the API keys. Administrator only. A key is write-only here: once saved it is never shown again, only "key set, ends
 * 1234". Leaving the key box empty keeps the stored key. */
@Component({
  selector: 'app-ai-provider-settings',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './ai-provider-settings.component.html',
  styleUrl: './ai-provider-settings.component.css'
})
export class AiProviderSettingsComponent implements OnInit {
  private readonly admin = inject(AiProviderAdminService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  /** Only roles holding settings.ai.manage (the Administrator) see this section. */
  readonly visible = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAi);
  });

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly providers = signal<AiProviderConfig[]>([]);
  readonly dirty = signal(false);

  /** Providers whose stored key will be removed on the next save. */
  readonly clearing = signal<AiProviderKind[]>([]);

  /** The outcome of Test connection, per provider. */
  readonly testing = signal<AiProviderKind | null>(null);
  readonly results = signal<Partial<Record<AiProviderKind, AiProviderTestResult>>>({});

  private forms = new Map<AiProviderKind, ProviderForm>();

  ngOnInit(): void {
    if (this.visible()) {
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.admin.getConfig().subscribe({
      next: (config) => {
        this.apply(config);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the AI assistant settings.'));
        this.loading.set(false);
      }
    });
  }

  form(kind: AiProviderKind): ProviderForm {
    return this.forms.get(kind)!;
  }

  isClearing(kind: AiProviderKind): boolean {
    return this.clearing().includes(kind);
  }

  /** What the key box says: whether a key is stored, never the key itself. */
  keyStatus(provider: AiProviderConfig): string {
    if (this.isClearing(provider.kind)) {
      return 'The stored key will be removed when you save.';
    }
    if (!provider.keySet) {
      return 'No key saved yet.';
    }
    if (!provider.keyReadable) {
      return 'The saved key can no longer be read. Enter it again.';
    }
    return provider.keyLast4 ? `A key is saved (ends …${provider.keyLast4}). Leave the box empty to keep it.` : 'A key is saved. Leave the box empty to keep it.';
  }

  clearKey(kind: AiProviderKind): void {
    this.form(kind).controls.apiKey.reset('');
    this.clearing.update((kinds) => [...kinds, kind]);
    this.dirty.set(true);
  }

  keepKey(kind: AiProviderKind): void {
    this.clearing.update((kinds) => kinds.filter((existing) => existing !== kind));
  }

  save(): void {
    this.saveError.set(null);
    if (this.saving() || !this.valid()) {
      return;
    }

    const updates: UpdateAiProvider[] = this.providers().map((provider) => {
      const value = this.form(provider.kind).getRawValue();
      const update: UpdateAiProvider = { kind: provider.kind, isEnabled: value.isEnabled, modelName: value.modelName.trim() };
      if (provider.kind === 'Ollama') {
        update.baseUrl = value.baseUrl.trim() || null;
      }

      const key = value.apiKey.trim();
      if (key) {
        update.apiKey = key;
      } else if (this.isClearing(provider.kind)) {
        update.clearKey = true;
      }

      return update;
    });

    this.saving.set(true);
    this.admin.save(updates).subscribe({
      next: (config) => {
        this.saving.set(false);
        this.apply(config);
        this.toast.success('AI assistant settings saved');
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.saveError.set(apiErrorMessage(err, 'Could not save the AI assistant settings.'));
      }
    });
  }

  test(kind: AiProviderKind): void {
    this.testing.set(kind);
    this.admin.test(kind).subscribe({
      next: (result) => {
        this.testing.set(null);
        this.results.update((results) => ({ ...results, [kind]: result }));
      },
      error: (err: unknown) => {
        this.testing.set(null);
        this.results.update((results) => ({
          ...results,
          [kind]: { ok: false, message: apiErrorMessage(err, 'The test could not be run.'), failureKind: null }
        }));
      }
    });
  }

  private valid(): boolean {
    let valid = true;
    for (const provider of this.providers()) {
      const form = this.form(provider.kind);
      form.markAllAsTouched();
      valid = valid && form.valid;
    }
    return valid;
  }

  /** Rebuilds the forms from what the server says. The key boxes always start empty. */
  private apply(config: AiProviderConfig[]): void {
    this.providers.set(config);
    this.clearing.set([]);
    this.dirty.set(false);
    this.forms = new Map(
      config.map((provider) => {
        const form: ProviderForm = new FormGroup({
          isEnabled: new FormControl(provider.isEnabled, { nonNullable: true }),
          modelName: new FormControl(provider.modelName, { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
          baseUrl: new FormControl(provider.baseUrl ?? '', { nonNullable: true, validators: [Validators.pattern(/^(https?:\/\/\S+)?$/i)] }),
          apiKey: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\S*$/), Validators.maxLength(500)] })
        });
        form.valueChanges.subscribe(() => this.dirty.set(true));
        return [provider.kind, form];
      })
    );
  }
}
