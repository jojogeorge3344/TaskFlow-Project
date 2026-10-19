import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { forkJoin } from 'rxjs';
import { IconComponent } from '../../core/layout/icon.component';
import { AccessRole, PermissionInfo } from '../../core/models';
import { RoleService } from '../../core/services/role.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';

/** What the right-hand pane shows: an existing role by id, or a role that has not been saved yet. */
type Selection = number | 'new' | null;

interface PermissionGroup {
  name: string;
  permissions: PermissionInfo[];
}

/**
 * Settings > Permissions Management. Administrator only. The left pane lists every role; the right pane shows the
 * permission checkboxes of the selected one. The three system roles are read-only (every box disabled, nothing to save
 * or delete); a custom role can be created, edited and deleted. Nothing is stored until Save is pressed.
 */
@Component({
  selector: 'app-permissions-management',
  standalone: true,
  imports: [IconComponent, ConfirmDialogComponent],
  templateUrl: './permissions-management.component.html',
  styleUrl: './permissions-management.component.css'
})
export class PermissionsManagementComponent implements OnInit {
  private readonly roleService = inject(RoleService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  readonly roles = signal<AccessRole[]>([]);
  readonly catalog = signal<PermissionInfo[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);

  readonly selection = signal<Selection>(null);
  readonly draftName = signal('');
  readonly draftKeys = signal<string[]>([]);
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly formError = signal<string | null>(null);
  readonly nameTouched = signal(false);

  readonly selectedRole = computed<AccessRole | null>(() => {
    const selection = this.selection();
    return typeof selection === 'number' ? (this.roles().find((role) => role.id === selection) ?? null) : null;
  });

  readonly isNew = computed(() => this.selection() === 'new');

  /** A system role is shown but never changed. */
  readonly isSystemRole = computed(() => this.selectedRole()?.isSystem === true);

  readonly editable = computed(() => this.isNew() || (this.selectedRole() !== null && !this.isSystemRole()));

  /** Permissions only the Administrator may hold are shown on that role but never offered for another role. */
  readonly groups = computed<PermissionGroup[]>(() => {
    const shown = this.editable() ? this.catalog().filter((permission) => permission.isAssignable) : this.catalog();
    const groups: PermissionGroup[] = [];
    for (const permission of shown) {
      let group = groups.find((candidate) => candidate.name === permission.group);
      if (!group) {
        group = { name: permission.group, permissions: [] };
        groups.push(group);
      }
      group.permissions.push(permission);
    }
    return groups;
  });

  readonly dirty = computed(() => {
    if (this.isNew()) {
      return this.draftName().trim() !== '' || this.draftKeys().length > 0;
    }

    const role = this.selectedRole();
    if (!role || role.isSystem) {
      return false;
    }

    return !sameKeys(this.draftKeys(), role.permissionKeys) || this.draftName().trim() !== role.name;
  });

  readonly nameError = computed(() =>
    this.nameTouched() && this.draftName().trim() === '' ? 'Enter a name for the role.' : null
  );

  /** How many boxes this role could have ticked: every permission for a system role, the assignable ones otherwise. */
  readonly totalCount = computed(() => this.groups().reduce((total, group) => total + group.permissions.length, 0));

  readonly heldCount = computed(() => {
    const role = this.selectedRole();
    return this.isSystemRole() && role ? role.permissionKeys.length : this.draftKeys().length;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    forkJoin({ roles: this.roleService.list(), catalog: this.roleService.permissions() }).subscribe({
      next: ({ roles, catalog }) => {
        this.roles.set(roles);
        this.catalog.set(catalog);
        this.loading.set(false);
        this.show(roles[0]?.id ?? null);
      },
      error: (err) => {
        this.loading.set(false);
        this.loadError.set(apiErrorMessage(err, 'Could not load roles and permissions.'));
      }
    });
  }

  isChecked(key: string): boolean {
    const role = this.selectedRole();
    return this.isSystemRole() && role ? role.permissionKeys.includes(key) : this.draftKeys().includes(key);
  }

  toggle(key: string, checked: boolean): void {
    if (!this.editable()) {
      return;
    }

    this.draftKeys.update((keys) => {
      const without = keys.filter((existing) => existing !== key);
      return checked ? [...without, key] : without;
    });
  }

  setName(value: string): void {
    this.draftName.set(value);
    this.formError.set(null);
  }

  /** Switches at once; only unsaved edits make it stop and ask first. */
  async select(roleId: number): Promise<void> {
    if (this.selection() === roleId) {
      return;
    }
    if (this.dirty() && !(await this.confirmDiscard())) {
      return;
    }
    this.show(roleId);
  }

  async startNew(): Promise<void> {
    if (this.isNew()) {
      return;
    }
    if (this.dirty() && !(await this.confirmDiscard())) {
      return;
    }
    this.show('new');
  }

  save(): void {
    if (!this.editable() || this.saving()) {
      return;
    }

    this.nameTouched.set(true);
    this.formError.set(null);
    const name = this.draftName().trim();
    if (name === '') {
      return;
    }

    const request = { name, permissionKeys: this.draftKeys() };
    const role = this.selectedRole();
    const request$ = role ? this.roleService.update(role.id, request) : this.roleService.create(request);

    this.saving.set(true);
    request$.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.roles.update((roles) =>
          roles.some((existing) => existing.id === saved.id)
            ? roles.map((existing) => (existing.id === saved.id ? saved : existing))
            : [...roles, saved]
        );
        this.show(saved.id);
        this.toast.success(role ? `${saved.name} saved` : `${saved.name} created`);
      },
      error: (err) => {
        // The Administrator's edits stay in the form so they can fix the problem and try again.
        this.saving.set(false);
        this.formError.set(apiErrorMessage(err, 'Could not save the role.'));
      }
    });
  }

  async remove(): Promise<void> {
    const role = this.selectedRole();
    if (!role || role.isSystem || this.deleting()) {
      return;
    }

    const confirmed = await this.confirmDialog().open({
      title: `Delete ${role.name}?`,
      message: 'The role and its permissions are removed. This cannot be undone.',
      confirmLabel: 'Delete role',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.deleting.set(true);
    this.formError.set(null);
    this.roleService.delete(role.id).subscribe({
      next: () => {
        this.deleting.set(false);
        const remaining = this.roles().filter((existing) => existing.id !== role.id);
        this.roles.set(remaining);
        this.show(remaining[0]?.id ?? null);
        this.toast.success(`${role.name} deleted`);
      },
      error: (err) => {
        // For example "2 users still hold this role": the role stays and the reason is shown.
        this.deleting.set(false);
        this.formError.set(apiErrorMessage(err, 'Could not delete the role.'));
      }
    });
  }

  /** Discards the draft and shows the stored state of a role (or an empty form for a new one). */
  private show(selection: Selection): void {
    this.selection.set(selection);
    this.formError.set(null);
    this.nameTouched.set(false);

    const role = typeof selection === 'number' ? this.roles().find((candidate) => candidate.id === selection) : undefined;
    this.draftName.set(role && !role.isSystem ? role.name : '');
    this.draftKeys.set(role && !role.isSystem ? [...role.permissionKeys] : []);
  }

  private confirmDiscard(): Promise<boolean> {
    return this.confirmDialog().open({
      title: 'Discard unsaved changes?',
      message: 'The changes you made to this role have not been saved and will be lost.',
      confirmLabel: 'Discard changes',
      variant: 'danger'
    });
  }
}

function sameKeys(left: string[], right: string[]): boolean {
  return left.length === right.length && [...left].sort().every((key, index) => key === [...right].sort()[index]);
}
