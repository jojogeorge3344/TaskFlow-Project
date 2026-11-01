import { Component, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { AiProviderConfig, AiProviderTestResult } from '../../../assistant/models/assistant.model';
import { AiProviderAdminService } from '../services/ai-provider-admin.service';
import { ModelRow, ProviderGroup } from './ai-provider-settings.model';

/** The body of the TFA Settings screen: every model of every provider as a read-only list, grouped by provider. Nothing
 * is typed here. "Use this model" switches TFA to a model in one step; Test connection tests a model with the saved key
 * or address; models an administrator added can be edited (on the Add AI model screen in edit mode) and deleted. The
 * model TFA uses cannot be edited or deleted: a notice says to switch first. Keys are never shown, only "ends 1234". */
@Component({
  selector: 'app-ai-provider-settings',
  standalone: true,
  imports: [RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './ai-provider-settings.component.html',
  styleUrl: './ai-provider-settings.component.css'
})
export class AiProviderSettingsComponent implements OnInit, OnDestroy {
  private readonly admin = inject(AiProviderAdminService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly dialog = viewChild.required(ConfirmDialogComponent);

  /** Only roles holding settings.ai.view see this section; use, test, edit and delete have their own permissions. */
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

  /** Adding or editing a model tests it before saving it, so both screens need edit and test. */
  readonly canAddModel = computed(() => this.canEdit() && this.canTest());

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly providers = signal<AiProviderConfig[]>([]);

  /** The row whose Use or Delete request is running, as `kind|model`. */
  readonly busy = signal<string | null>(null);

  /** The row whose test is running, and each row's last test result, keyed `kind|model`. */
  readonly testing = signal<string | null>(null);
  readonly results = signal<Record<string, AiProviderTestResult>>({});

  readonly groups = computed<ProviderGroup[]>(() =>
    this.providers().map((provider) => ({
      provider,
      rows: (provider.models ?? []).map((model) => ({
        provider,
        model,
        added: (provider.customModels ?? []).includes(model),
        inUse: provider.isEnabled && provider.modelName === model,
        ready: !provider.requiresKey || (provider.keySet && provider.keyReadable)
      }))
    }))
  );

  /** The model TFA answers with, for the line above the list. */
  readonly inUse = computed(() => this.groups().flatMap((group) => group.rows).find((row) => row.inUse) ?? null);

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
        this.providers.set(config);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(apiErrorMessage(err, 'Could not load the TFA settings.'));
        this.loading.set(false);
      }
    });
  }

  rowKey(row: ModelRow): string {
    return `${row.provider.providerId}|${row.model}`;
  }

  /** What the row says about the provider's key or address. Never the key itself. A custom provider shows its address,
   * and its key when one is saved (a local server may need none). */
  keyText(provider: AiProviderConfig): string {
    if (!provider.requiresKey) {
      const address = provider.baseUrl || 'http://localhost:11434';
      return provider.keySet && provider.keyLast4 ? `${address} · key ends …${provider.keyLast4}` : address;
    }
    if (!provider.keySet) {
      return 'No key saved';
    }
    if (!provider.keyReadable) {
      return 'The saved key can no longer be read';
    }
    return provider.keyLast4 ? `Key saved, ends …${provider.keyLast4}` : 'Key saved';
  }

  noKeyHint(provider: AiProviderConfig): string {
    return `No key saved for ${provider.displayName} yet. Add it on the Add AI model screen.`;
  }

  /** "Use this model": TFA switches to it at once; the other providers are switched off with their keys kept. */
  use(row: ModelRow): void {
    if (row.inUse || !row.ready || this.busy()) {
      return;
    }

    this.actionError.set(null);
    this.busy.set(this.rowKey(row));
    this.admin.useModel(row.provider.providerId, row.model).subscribe({
      next: (config) => {
        this.busy.set(null);
        this.providers.set(config);
        this.toast.success(`${row.model} on ${row.provider.displayName} is now in use`);
      },
      error: (err: unknown) => {
        this.busy.set(null);
        this.actionError.set(apiErrorMessage(err, 'Could not switch TFA to that model.'));
      }
    });
  }

  /** Tests the row's model with the saved key or address. Nothing is saved. Clicking again starts it over. */
  test(row: ModelRow): void {
    const key = this.rowKey(row);
    this.testRun?.unsubscribe();
    this.testing.set(key);
    this.testRun = this.admin.test(row.provider.providerId, row.model).subscribe({
      next: (result) => {
        this.testing.set(null);
        this.results.update((results) => ({ ...results, [key]: result }));
      },
      error: (err: unknown) => {
        this.testing.set(null);
        this.results.update((results) => ({
          ...results,
          [key]: { ok: false, message: apiErrorMessage(err, 'The test could not be run.'), failureKind: null }
        }));
      }
    });
  }

  /** Opens the edit screen, unless TFA uses this model: then a notice says to switch first. */
  async edit(row: ModelRow): Promise<void> {
    if (row.inUse) {
      await this.notifyInUse(row, 'edit');
      return;
    }

    void this.router.navigate(['/settings/tfa/models', row.provider.providerId, row.model, 'edit']);
  }

  /** Deletes an added model after confirmation, unless TFA uses it: then a notice says to switch first. */
  async remove(row: ModelRow): Promise<void> {
    if (row.inUse) {
      await this.notifyInUse(row, 'delete');
      return;
    }

    const confirmed = await this.dialog().open({
      title: `Delete ${row.model}?`,
      message: `${row.model} is removed from the ${row.provider.displayName} models. The ${row.provider.displayName} key stays saved.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed || this.busy()) {
      return;
    }

    this.actionError.set(null);
    this.busy.set(this.rowKey(row));
    this.admin.removeModel(row.provider.providerId, row.model).subscribe({
      next: (config) => {
        this.busy.set(null);
        this.providers.set(config);
        this.toast.success(`Deleted ${row.model}`);
      },
      error: (err: unknown) => {
        this.busy.set(null);
        this.actionError.set(apiErrorMessage(err, 'Could not delete the model.'));
      }
    });
  }

  private notifyInUse(row: ModelRow, action: 'edit' | 'delete'): Promise<boolean> {
    return this.dialog().open({
      title: 'This model is in use',
      message: `${row.model} is in use by TFA. Switch TFA to another model first, then ${action} it.`,
      confirmLabel: 'OK',
      notice: true
    });
  }
}
