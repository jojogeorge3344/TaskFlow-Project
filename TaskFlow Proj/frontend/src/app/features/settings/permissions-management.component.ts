import { Component, HostListener, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { AccessRole, PermissionInfo } from '../../core/models';
import { RoleService } from '../../core/services/role.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';
import {
  CheckState,
  TreeFeature,
  TreeModule,
  TreeScreen,
  buildTree,
  changeCount,
  effectiveKeys,
  enabledCount,
  featuresOf,
  featuresOfScreen,
  isEnabled,
  isSelectable,
  restrictForCaller,
  matches,
  scopeLevel,
  setMany,
  setScope,
  stateOf,
  toggleKey
} from './permission-tree';
import { Selection, EnabledFilter, PermissionView, ModuleView, ScreenView, FeatureView } from './permissions-management.model';

/**
 * Settings > Role Configuration. Administrator only. Every role is shown against the same module-based permission
 * tree (Module > Screen > Section > Feature > Action); only what each role holds differs. The three system roles are
 * read-only; a custom role can be created, copied from another role, edited and deleted. Nothing is stored until Save.
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
  private readonly auth = inject(AuthService);
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

  readonly roleSearch = signal('');
  readonly search = signal('');
  readonly filter = signal<EnabledFilter>('all');
  readonly view = signal<PermissionView>('tree');
  readonly activeModule = signal<string | null>(null);
  readonly collapsed = signal<ReadonlySet<string>>(new Set());
  /** On narrow screens the role list and the permissions are two steps. */
  readonly showDetail = signal(false);

  readonly selectedRole = computed<AccessRole | null>(() => {
    const selection = this.selection();
    return typeof selection === 'number' ? (this.roles().find((role) => role.id === selection) ?? null) : null;
  });

  readonly isNew = computed(() => this.selection() === 'new');

  /** A system role is shown but never changed. */
  readonly isSystemRole = computed(() => this.selectedRole()?.isSystem === true);

  readonly editable = computed(() => this.canModifyAccess() && (this.isNew() || (this.selectedRole() !== null && !this.isSystemRole())));

  readonly canViewAllRoles = computed(() => this.auth.hasPermission(Permissions.RolesViewAll));
  readonly canModifyAccess = computed(() => this.auth.hasPermission(Permissions.RolesModifyAccess));

  readonly filteredRoles = computed(() => {
    const term = this.roleSearch().trim().toLowerCase();
    let roles = this.roles();
    if (!this.canViewAllRoles()) {
      const currentRole = this.auth.currentUser()?.role;
      roles = roles.filter((role) => role.name === currentRole);
    }
    return term === '' ? roles : roles.filter((role) => role.name.toLowerCase().includes(term));
  });

  readonly tree = computed<TreeModule[]>(() => buildTree(this.catalog()));

  /** What is ticked right now: the stored grants of a system role, the draft of anything editable. */
  readonly shownKeys = computed<string[]>(() => {
    const role = this.selectedRole();
    return this.isSystemRole() && role ? role.permissionKeys : this.draftKeys();
  });

  readonly effective = computed(() => effectiveKeys(this.shownKeys(), this.catalog()));

  /** Permissions an administrator can see for this role: Administrator-only entries are hidden while editing a custom role. */
  private readonly listed = computed(() =>
    this.catalog().filter((permission) => !this.editable() || permission.isAssignable || permission.alwaysGranted)
  );

  readonly totalCount = computed(() => this.listed().length);
  readonly heldCount = computed(() => enabledCount(this.listed(), this.effective()));
  readonly heldPercent = computed(() => (this.totalCount() === 0 ? 0 : Math.round((this.heldCount() / this.totalCount()) * 100)));

  readonly modules = computed<ModuleView[]>(() => this.tree().map((module) => this.moduleView(module)));

  /** The navigator lists every module; the panel shows the active one, or every module with a match while searching. */
  readonly searching = computed(() => this.search().trim() !== '' || this.filter() !== 'all');

  readonly panelModules = computed<ModuleView[]>(() => {
    const all = this.modules();
    if (this.searching()) {
      return all.map((module) => this.filterModule(module)).filter((module): module is ModuleView => module !== null);
    }

    const active = this.activeModule() ?? all[0]?.name;
    return all.filter((module) => module.name === active);
  });

  readonly matchCount = computed(() =>
    this.panelModules().reduce((total, module) => total + module.screens.reduce((sum, screen) => sum + screen.sections.reduce(
      (inner, section) => inner + section.features.length, 0), 0), 0)
  );

  readonly dirty = computed(() => {
    if (this.isNew()) {
      return this.draftName().trim() !== '' || this.draftKeys().length > 0;
    }

    const role = this.selectedRole();
    if (!role || role.isSystem) {
      return false;
    }

    return changeCount(this.storedKeys(role), this.draftKeys()) > 0 || this.draftName().trim() !== role.name;
  });

  readonly changes = computed(() => {
    const role = this.selectedRole();
    const base = role && !role.isSystem ? this.storedKeys(role) : [];
    return changeCount(base, this.draftKeys()) + (role && this.draftName().trim() !== role.name ? 1 : 0);
  });

  readonly nameError = computed(() =>
    this.nameTouched() && this.draftName().trim() === '' ? 'Enter a name for the role.' : null
  );

  /** Rows of the role-by-permission matrix, grouped by module. */
  readonly matrixGroups = computed(() =>
    this.tree().map((module) => ({
      name: module.name,
      rows: module.screens.flatMap((screen) =>
        screen.sections.flatMap((section) =>
          section.features.flatMap((feature) =>
            feature.permissions.map((permission) => ({
              permission,
              label: feature.isScope ? `${feature.name} - ${scopeWord(permission)}` : permission.name,
              path: [screen.name, section.name].filter((part) => part !== '').join(' / ')
            }))
          )
        )
      )
    }))
  );

  readonly matrixRoles = computed(() =>
    this.roles().map((role) => ({
      role,
      keys: effectiveKeys(
        this.selectedRole()?.id === role.id && !role.isSystem ? this.draftKeys() : role.permissionKeys,
        this.catalog()
      )
    }))
  );

  ngOnInit(): void {
    this.load();
  }

  @HostListener('window:beforeunload', ['$event'])
  warnBeforeLeaving(event: BeforeUnloadEvent): void {
    if (this.dirty()) {
      event.preventDefault();
    }
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(null);

    forkJoin({ roles: this.roleService.list(), catalog: this.roleService.permissions() }).subscribe({
      next: ({ roles, catalog }) => {
        this.roles.set(roles);
        this.catalog.set(restrictForCaller(catalog, this.auth.hasPermission(Permissions.RolesAdministratorAssign)));
        this.loading.set(false);
        this.activeModule.set(buildTree(catalog)[0]?.name ?? null);
        this.show(roles[0]?.id ?? null);
      },
      error: (err) => {
        this.loading.set(false);
        this.loadError.set(apiErrorMessage(err, 'Could not load roles and permissions.'));
      }
    });
  }

  isChecked(key: string): boolean {
    return this.effective().has(key);
  }

  /** True when the permission is on for the role, including features every user has. */
  isOn(permission: PermissionInfo): boolean {
    return isEnabled(permission, this.effective());
  }

  isLocked(permission: PermissionInfo): boolean {
    return permission.alwaysGranted || !this.editable() || !isSelectable(permission);
  }

  toggle(key: string, checked: boolean): void {
    if (!this.editable()) {
      return;
    }

    this.draftKeys.update((keys) => toggleKey(keys, key, checked, this.catalog()));
  }

  chooseScope(feature: TreeFeature, level: PermissionInfo): void {
    if (!this.editable()) {
      return;
    }

    this.draftKeys.update((keys) => setScope(keys, feature, level, this.catalog()));
  }

  toggleScreen(module: ModuleView, screen: ScreenView, checked: boolean): void {
    this.setGroup(this.screenPermissions(module.name, screen.name), checked);
  }

  toggleModule(module: ModuleView, checked: boolean): void {
    const tree = this.tree().find((candidate) => candidate.name === module.name);
    this.setGroup(tree ? featuresOf(tree).flatMap((feature) => feature.permissions) : [], checked);
  }

  /** Select all / Deselect all: every permission this role may be given. */
  setAll(checked: boolean): void {
    this.setGroup(this.catalog(), checked);
  }

  setSearch(value: string): void {
    this.search.set(value);
  }

  setFilter(value: EnabledFilter): void {
    this.filter.set(value);
  }

  openModule(name: string): void {
    this.activeModule.set(name);
    this.search.set('');
    this.filter.set('all');
  }

  isCollapsed(screen: ScreenView): boolean {
    return this.collapsed().has(screen.key);
  }

  toggleCollapsed(screen: ScreenView): void {
    this.collapsed.update((current) => {
      const next = new Set(current);
      if (!next.delete(screen.key)) {
        next.add(screen.key);
      }
      return next;
    });
  }

  collapseAll(): void {
    this.collapsed.set(new Set(this.tree().flatMap((module) => module.screens.map((screen) => `${module.name}/${screen.name}`))));
  }

  expandAll(): void {
    this.collapsed.set(new Set());
  }

  setName(value: string): void {
    this.draftName.set(value);
    this.formError.set(null);
  }

  /** Replaces the draft with another role's permissions. Only what an editable role can hold is copied. */
  copyFrom(roleId: number): void {
    const source = this.roles().find((role) => role.id === roleId);
    if (!source || !this.editable()) {
      return;
    }

    const allowed = new Set(this.catalog().filter(isSelectable).map((permission) => permission.key));
    this.draftKeys.set(source.permissionKeys.filter((key) => allowed.has(key)));
  }

  /** Switches at once; only unsaved edits make it stop and ask first. */
  async select(roleId: number): Promise<void> {
    if (this.selection() === roleId) {
      this.showDetail.set(true);
      return;
    }
    if (this.dirty() && !(await this.confirmDiscard())) {
      return;
    }
    this.show(roleId);
    this.showDetail.set(true);
  }

  async startNew(): Promise<void> {
    if (this.isNew()) {
      this.showDetail.set(true);
      return;
    }
    if (this.dirty() && !(await this.confirmDiscard())) {
      return;
    }
    this.show('new');
    this.showDetail.set(true);
  }

  /** A system role cannot change, but its permissions are a fine start for a custom role. */
  async duplicate(): Promise<void> {
    const source = this.selectedRole();
    if (!source) {
      return;
    }
    if (this.dirty() && !(await this.confirmDiscard())) {
      return;
    }

    this.show('new');
    this.draftName.set(`${source.name} copy`);
    this.copyFrom(source.id);
  }

  async discard(): Promise<void> {
    if (!this.dirty()) {
      return;
    }

    const selection = this.selection();
    if (await this.confirmDiscard()) {
      this.show(selection === 'new' ? 'new' : selection);
    }
  }

  backToRoles(): void {
    this.showDetail.set(false);
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
        this.showDetail.set(false);
        this.toast.success(`${role.name} deleted`);
      },
      error: (err) => {
        // For example "2 users still hold this role": the role stays and the reason is shown.
        this.deleting.set(false);
        this.formError.set(apiErrorMessage(err, 'Could not delete the role.'));
      }
    });
  }

  // ------------------------------------------------------------------ view building

  /** The keys a stored role holds that an editor can tick: stored roles can carry scope levels implied by a higher one. */
  private storedKeys(role: AccessRole): string[] {
    const selectable = new Set(this.catalog().filter(isSelectable).map((permission) => permission.key));
    return role.permissionKeys.filter((key) => selectable.has(key));
  }

  private screenPermissions(moduleName: string, screenName: string): PermissionInfo[] {
    const screen = this.tree().find((module) => module.name === moduleName)?.screens.find((candidate) => candidate.name === screenName);
    return screen ? featuresOfScreen(screen).flatMap((feature) => feature.permissions) : [];
  }

  private setGroup(permissions: PermissionInfo[], checked: boolean): void {
    if (!this.editable()) {
      return;
    }

    this.draftKeys.update((keys) => setMany(keys, permissions, checked, this.catalog()));
  }

  private moduleView(module: TreeModule): ModuleView {
    const effective = this.effective();
    const screens = module.screens.map((screen) => this.screenView(module.name, screen));
    const permissions = featuresOf(module).flatMap((feature) => feature.permissions).filter((permission) => this.isListed(permission));

    return {
      name: module.name,
      screens,
      enabled: enabledCount(permissions, effective),
      total: permissions.length,
      state: stateOf(permissions, effective),
      selectable: permissions.some(isSelectable)
    };
  }

  private screenView(moduleName: string, screen: TreeScreen): ScreenView {
    const effective = this.effective();
    const sections = screen.sections
      .map((section) => ({
        name: section.name,
        features: section.features
          .filter((feature) => feature.permissions.some((permission) => this.isListed(permission)))
          .map<FeatureView>((feature) => ({
            feature,
            state: stateOf(feature.permissions, effective),
            level: scopeLevel(feature, effective)
          }))
      }))
      .filter((section) => section.features.length > 0);
    const permissions = featuresOfScreen(screen).flatMap((feature) => feature.permissions).filter((permission) => this.isListed(permission));

    return {
      name: screen.name,
      key: `${moduleName}/${screen.name}`,
      sections,
      enabled: enabledCount(permissions, effective),
      total: permissions.length,
      state: stateOf(permissions, effective),
      selectable: permissions.some(isSelectable)
    };
  }

  private isListed(permission: PermissionInfo): boolean {
    return !this.editable() || permission.isAssignable || permission.alwaysGranted;
  }

  /** Keeps the features that match the search and the enabled filter; a module with none left is dropped. */
  private filterModule(module: ModuleView): ModuleView | null {
    const term = this.search();
    const mode = this.filter();
    const effective = this.effective();

    const keepFeature = (view: FeatureView): boolean => {
      const textMatch = view.feature.permissions.some((permission) => matches(permission, term))
        || matches({ ...view.feature.permissions[0], name: view.feature.name, description: view.feature.description }, term);
      const on = view.feature.permissions.some((permission) => isEnabled(permission, effective));
      const off = view.feature.permissions.some((permission) => !isEnabled(permission, effective));
      return textMatch && (mode === 'all' || (mode === 'enabled' ? on : off));
    };

    const screens = module.screens
      .map((screen) => ({
        ...screen,
        sections: screen.sections
          .map((section) => ({ ...section, features: section.features.filter(keepFeature) }))
          .filter((section) => section.features.length > 0)
      }))
      .filter((screen) => screen.sections.length > 0);

    return screens.length === 0 ? null : { ...module, screens };
  }

  /** Discards the draft and shows the stored state of a role (or an empty form for a new one). */
  private show(selection: Selection): void {
    this.selection.set(selection);
    this.formError.set(null);
    this.nameTouched.set(false);
    this.search.set('');
    this.filter.set('all');

    const role = typeof selection === 'number' ? this.roles().find((candidate) => candidate.id === selection) : undefined;
    this.draftName.set(role && !role.isSystem ? role.name : '');
    this.draftKeys.set(role && !role.isSystem ? this.storedKeys(role) : []);
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

/** Short label for a level of a scope selector. */
export function scopeWord(permission: PermissionInfo): string {
  const last = permission.key.split('.').pop() ?? permission.key;
  if (permission.alwaysGranted) {
    return 'own';
  }
  return last === 'all' ? 'all' : last;
}
