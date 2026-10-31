import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { AiProviderConfig, AiProviderKind, AiProviderTestResult, UpdateAiProvider } from '../../../assistant/models/assistant.model';
import { AiProviderAdminService } from '../services/ai-provider-admin.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ToastService } from '../../../../shared/services/toast.service';
import { ProviderForm } from './ai-provider-settings.model';

/** The dropdown entry that lets the administrator type a model that is not in the list yet. */
export const ADD_MODEL = '__add__';

/** Where each cloud provider's server is. Ollama's address is the one saved in Settings. */
export const CLOUD_SERVERS: Partial<Record<AiProviderKind, string>> = {
  Gemini: 'generativelanguage.googleapis.com',
  OpenAI: 'api.openai.com'
};

/** When "Add new model…" is chosen, the typed name is required and short enough to store. */
function newModelRequired(group: AbstractControl): ValidationErrors | null {
  if (group.get('modelName')?.value !== ADD_MODEL) {
    return null;
  }

  const name = String(group.get('newModel')?.value ?? '').trim();
  return name.length === 0 || name.length > 100 ? { newModel: true } : null;
}

/** The body of the TFA Settings screen: the model and server TFA uses, the Ollama address and the API keys.
 * The Model and Server dropdowns are linked. The model and server chosen and saved here is the only one TFA offers in its
 * window: saving enables the chosen server and switches the others off (their keys and models stay stored). A dropdown
 * picks the server and shows only its own fields: the address for Ollama, the API key for a cloud server. The Model
 * dropdown lists the models kept in the database, with "Add new model…". Administrator only. A key is write-only here:
 * once saved it is never shown again, only "key set, ends 1234". Leaving the key box empty keeps the stored key. */
@Component({
  selector: 'app-ai-provider-settings',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './ai-provider-settings.component.html',
  styleUrl: './ai-provider-settings.component.css'
})
export class AiProviderSettingsComponent implements OnInit, OnDestroy {
  private readonly admin = inject(AiProviderAdminService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  /** Only roles holding settings.ai.view see this section; edit, test and remove-model have their own permissions. */
  readonly visible = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAiView);
  });

  readonly canEdit = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAiEdit);
  });

  readonly canTest = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAiTest);
  });

  readonly mayRemoveModel = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAiRemoveModel);
  });

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);
  readonly providers = signal<AiProviderConfig[]>([]);
  readonly dirty = signal(false);
  readonly addModelValue = ADD_MODEL;
  readonly removing = signal(false);

  /** The server TFA uses now, as saved. */
  readonly activeKind = signal<AiProviderKind | null>(null);

  /** The provider whose fields are shown. */
  readonly selectedKind = signal<AiProviderKind | null>(null);
  readonly selected = computed(() => {
    const all = this.providers();
    return all.find((provider) => provider.kind === this.selectedKind()) ?? all.at(0) ?? null;
  });

  /** True when what is on screen is not what TFA uses yet: something was edited, or another server was chosen. */
  readonly needsSave = computed(() => {
    const chosen = this.selected()?.kind ?? null;
    return this.dirty() || (chosen !== null && chosen !== this.activeKind());
  });

  /** Providers whose stored key will be removed on the next save. */
  readonly clearing = signal<AiProviderKind[]>([]);

  /** The outcome of Test connection, per provider. */
  readonly testing = signal<AiProviderKind | null>(null);
  readonly results = signal<Partial<Record<AiProviderKind, AiProviderTestResult>>>({});

  private forms = new Map<AiProviderKind, ProviderForm>();
  /** The test that is running, so a new click can replace it. */
  private testRun: Subscription | null = null;

  ngOnInit(): void {
    if (this.visible()) {
      this.load();
    }
  }

  ngOnDestroy(): void {
    this.testRun?.unsubscribe();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.admin.getConfig().subscribe({
      next: (config) => {
        this.apply(config, true);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the TFA settings.'));
        this.loading.set(false);
      }
    });
  }

  form(kind: AiProviderKind): ProviderForm {
    return this.forms.get(kind)!;
  }

  /** Choosing a server shows that server's own model, so the Model dropdown follows. */
  selectKind(kind: string): void {
    this.selectedKind.set(kind as AiProviderKind);
  }

  /** The Model dropdown lists the models of every server. Choosing one also selects the server it belongs to, so the
   * Server dropdown follows. */
  chooseModel(value: string): void {
    const separator = value.indexOf('|');
    const kind = value.slice(0, separator) as AiProviderKind;
    const name = value.slice(separator + 1);
    if (!this.forms.has(kind)) {
      return;
    }

    this.selectedKind.set(kind);
    this.form(kind).controls.modelName.setValue(name);
  }

  /** The models listed for a server. The chosen model is always in the list, even if the server sent no list, so a
   * server never looks empty. */
  modelChoices(provider: AiProviderConfig): string[] {
    const listed = provider.models ?? [];
    return provider.modelName && !listed.includes(provider.modelName) ? [...listed, provider.modelName] : listed;
  }

  /** The value of a model entry in the Model dropdown: the server it belongs to and its name. */
  modelOption(kind: AiProviderKind, name: string): string {
    return `${kind}|${name}`;
  }

  /** True for the entry that is currently chosen: the selected server's chosen model. */
  isChosenModel(kind: AiProviderKind, name: string): boolean {
    return this.selected()?.kind === kind && this.form(kind).controls.modelName.value === name;
  }

  /** What the Server dropdown shows for a provider: Ollama's saved address, or the cloud provider's own server. */
  serverLabel(provider: AiProviderConfig): string {
    if (provider.kind === 'Ollama') {
      return this.form(provider.kind).controls.baseUrl.value.trim() || 'http://localhost:11434';
    }

    return CLOUD_SERVERS[provider.kind] ?? provider.displayName;
  }

  /** The model that is chosen for a server on screen: the one picked, or the new name being typed. */
  chosenModelName(provider: AiProviderConfig): string {
    const controls = this.form(provider.kind).controls;
    if (controls.modelName.value === ADD_MODEL) {
      return controls.newModel.value.trim() || 'a new model';
    }

    return controls.modelName.value;
  }

  /** What the status next to a server says: whether TFA uses it, and if not, whether it could. */
  statusText(provider: AiProviderConfig): string {
    if (provider.kind === this.activeKind()) {
      return provider.isConfigured ? 'In use' : 'Not configured';
    }

    return provider.requiresKey && (!provider.keySet || !provider.keyReadable) ? 'Needs an API key' : 'Ready to use';
  }

  isReady(provider: AiProviderConfig): boolean {
    const text = this.statusText(provider);
    return text === 'In use' || text === 'Ready to use';
  }

  /** True while "Add new model…" is chosen, so the box for the new name is shown. */
  isAddingModel(kind: AiProviderKind): boolean {
    return this.form(kind).controls.modelName.value === ADD_MODEL;
  }

  /** True when the model picked for this server was added by an administrator and is not the one saved for it, so it can
   * be taken out of the list. Built-in models and the saved one stay. */
  canRemoveModel(provider: AiProviderConfig): boolean {
    const name = this.form(provider.kind).controls.modelName.value;
    return name !== ADD_MODEL && (provider.customModels ?? []).includes(name) && name !== provider.modelName;
  }

  /** Removes the picked model from the list. This reloads the screen from the server, so other unsaved edits are dropped. */
  removeModel(provider: AiProviderConfig): void {
    if (!this.canRemoveModel(provider) || this.removing()) {
      return;
    }

    const name = this.form(provider.kind).controls.modelName.value;
    this.saveError.set(null);
    this.removing.set(true);
    this.admin.removeModel(provider.kind, name).subscribe({
      next: (config) => {
        this.removing.set(false);
        this.apply(config);
        this.toast.success(`Removed ${name} from the list`);
      },
      error: (err: unknown) => {
        this.removing.set(false);
        this.saveError.set(apiErrorMessage(err, 'Could not remove the model.'));
      }
    });
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

  /** Saves every provider's settings. `afterSave` runs once the save has worked (used by Test connection). */
  save(afterSave?: () => void): void {
    this.saveError.set(null);
    if (this.saving() || !this.valid()) {
      return;
    }

    const updates: UpdateAiProvider[] = this.providers().map((provider) => {
      const value = this.form(provider.kind).getRawValue();
      const modelName = (value.modelName === ADD_MODEL ? value.newModel : value.modelName).trim();
      const update: UpdateAiProvider = { kind: provider.kind, isEnabled: provider.kind === this.selected()?.kind, modelName };
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
        this.toast.success('TFA settings saved');
        afterSave?.();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.saveError.set(apiErrorMessage(err, 'Could not save the TFA settings.'));
      }
    });
  }

  /** Test connection is always available. The test uses the saved settings, so any unsaved changes are saved first
   * (checked like Save is). Clicking again while a test is running starts it over. */
  test(kind: AiProviderKind): void {
    this.testRun?.unsubscribe();
    this.testRun = null;
    this.testing.set(null);

    if (this.needsSave()) {
      this.save(() => this.runTest(kind));
      return;
    }

    this.runTest(kind);
  }

  private runTest(kind: AiProviderKind): void {
    this.testing.set(kind);
    this.testRun = this.admin.test(kind).subscribe({
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
    let firstInvalid: AiProviderKind | null = null;
    for (const provider of this.providers()) {
      const form = this.form(provider.kind);
      form.markAllAsTouched();
      if (form.invalid && firstInvalid === null) {
        firstInvalid = provider.kind;
      }
    }

    if (firstInvalid !== null) {
      // The problem may be in a provider that is not on screen: show it.
      this.selectedKind.set(firstInvalid);
      return false;
    }

    return true;
  }

  /** Rebuilds the forms from what the server says. The key boxes always start empty. */
  private apply(config: AiProviderConfig[], resetSelection = false): void {
    this.providers.set(config);
    const active = (config.find((provider) => provider.isEnabled && provider.isConfigured) ?? config.find((provider) => provider.isEnabled))?.kind ?? null;
    this.activeKind.set(active);
    if (resetSelection || this.selectedKind() === null) {
      this.selectedKind.set(active ?? config.at(0)?.kind ?? null);
    }

    this.clearing.set([]);
    this.dirty.set(false);
    this.forms = new Map(
      config.map((provider) => {
        const form: ProviderForm = new FormGroup({
          modelName: new FormControl(provider.modelName, { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
          baseUrl: new FormControl(provider.baseUrl ?? '', { nonNullable: true, validators: [Validators.pattern(/^(https?:\/\/\S+)?$/i)] }),
          newModel: new FormControl('', { nonNullable: true }),
          apiKey: new FormControl('', { nonNullable: true, validators: [Validators.pattern(/^\S*$/), Validators.maxLength(500)] })
        }, { validators: newModelRequired });
        form.valueChanges.subscribe(() => this.dirty.set(true));
        return [provider.kind, form];
      })
    );
  }
}
