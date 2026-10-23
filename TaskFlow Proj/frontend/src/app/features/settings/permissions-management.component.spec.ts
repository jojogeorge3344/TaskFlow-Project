import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AccessRole, PermissionInfo } from '../../core/models';
import { RoleService } from '../../core/services/role.service';
import { ToastService } from '../../shared/ui/toast.service';
import { PermissionsManagementComponent } from './permissions-management.component';

interface Def {
  key: string;
  name: string;
  module: string;
  screen: string;
  section?: string;
  action: string;
  assignable?: boolean;
  always?: boolean;
  requires?: string;
  scope?: [string, number];
}

const DEFS: Def[] = [
  { key: 'dashboard.view.own', name: 'View my dashboard', module: 'Dashboard', screen: 'Dashboard', section: 'Overview', action: 'View', assignable: false, always: true, scope: ['dashboard.view', 1] },
  { key: 'dashboard.view.team', name: 'View team dashboard', module: 'Dashboard', screen: 'Dashboard', section: 'Overview', action: 'View', scope: ['dashboard.view', 2] },
  { key: 'dashboard.view.organization', name: 'View organization dashboard', module: 'Dashboard', screen: 'Dashboard', section: 'Overview', action: 'View', scope: ['dashboard.view', 3] },
  { key: 'dashboard.analytics.view', name: 'View charts and reports', module: 'Dashboard', screen: 'Dashboard', section: 'Analytics', action: 'Access' },
  { key: 'tasks.view.own', name: 'View my tasks', module: 'Tasks', screen: 'Task list', action: 'View', assignable: false, always: true, scope: ['tasks.view', 1] },
  { key: 'tasks.view.all', name: 'View all tasks', module: 'Tasks', screen: 'Task list', action: 'View', scope: ['tasks.view', 2] },
  { key: 'tasks.create', name: 'Create tasks', module: 'Tasks', screen: 'Task list', action: 'Create' },
  { key: 'tasks.delete', name: 'Delete tasks', module: 'Tasks', screen: 'Task list', action: 'Delete' },
  { key: 'projects.view', name: 'View projects', module: 'Projects', screen: 'Project list', action: 'View' },
  { key: 'projects.create', name: 'Create projects', module: 'Projects', screen: 'Project list', action: 'Create', requires: 'projects.view' },
  { key: 'projects.delete', name: 'Delete projects', module: 'Projects', screen: 'Project list', action: 'Delete', requires: 'projects.view' },
  { key: 'reports.view', name: 'View reports', module: 'Reports and workload', screen: 'Reports', action: 'View' },
  { key: 'reports.export', name: 'Export reports', module: 'Reports and workload', screen: 'Reports', action: 'Export', requires: 'reports.view' },
  { key: 'workload.view', name: 'View workload', module: 'Reports and workload', screen: 'Employee workload', action: 'View' },
  { key: 'mail.read', name: 'Read mail', module: 'Mail', screen: 'Mailbox', action: 'View', assignable: false, always: true },
  { key: 'roles.manage', name: 'Manage roles and permissions', module: 'Role configuration', screen: 'Role configuration', action: 'Manage', assignable: false }
];

const CATALOG: PermissionInfo[] = DEFS.map((def, index) => ({
  id: def.always ? 0 : index + 1,
  key: def.key,
  name: def.name,
  description: `Lets a role use ${def.key}.`,
  group: def.module,
  isAssignable: def.assignable ?? true,
  module: def.module,
  screen: def.screen,
  section: def.section ?? '',
  action: def.action,
  requires: def.requires ?? null,
  scopeGroup: def.scope?.[0] ?? null,
  scopeRank: def.scope?.[1] ?? 0,
  alwaysGranted: def.always ?? false
}));

const STORED = CATALOG.filter((permission) => !permission.alwaysGranted).map((permission) => permission.key);
const SELECTABLE = CATALOG.filter((permission) => permission.isAssignable && !permission.alwaysGranted).map((permission) => permission.key);

const MANAGER_KEYS = [
  'dashboard.view.team', 'dashboard.analytics.view', 'tasks.view.all', 'tasks.create', 'projects.view',
  'reports.view', 'reports.export', 'workload.view'
];

function role(id: number, name: string, isSystem: boolean, userCount: number, permissionKeys: string[]): AccessRole {
  return { id, name, isSystem, userCount, permissionKeys };
}

function seedRoles(): AccessRole[] {
  return [
    role(1, 'Administrator', true, 1, STORED),
    role(2, 'Manager', true, 1, MANAGER_KEYS),
    role(3, 'Employee', true, 4, ['dashboard.analytics.view']),
    role(4, 'Team Lead', false, 0, ['tasks.create', 'reports.view']),
    role(5, 'Busy Role', false, 2, ['workload.view'])
  ];
}

describe('PermissionsManagementComponent', () => {
  let listSpy: jasmine.Spy;
  let permissionsSpy: jasmine.Spy;
  let createSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    listSpy = jasmine.createSpy('list').and.returnValue(of(seedRoles()));
    permissionsSpy = jasmine.createSpy('permissions').and.returnValue(of(CATALOG));
    createSpy = jasmine.createSpy('create');
    updateSpy = jasmine.createSpy('update');
    deleteSpy = jasmine.createSpy('delete');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  function create(): ComponentFixture<PermissionsManagementComponent> {
    TestBed.configureTestingModule({
      imports: [PermissionsManagementComponent],
      providers: [
        {
          provide: RoleService,
          useValue: { list: listSpy, permissions: permissionsSpy, create: createSpy, update: updateSpy, delete: deleteSpy }
        },
        { provide: ToastService, useValue: toast }
      ]
    });
    const fixture = TestBed.createComponent(PermissionsManagementComponent);
    fixture.detectChanges();
    return fixture;
  }

  type Fixture = ComponentFixture<PermissionsManagementComponent>;

  function el(fixture: Fixture): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function box(fixture: Fixture, key: string): HTMLInputElement | null {
    return el(fixture).querySelector(`input[data-permission="${key}"]`);
  }

  function boxes(fixture: Fixture): HTMLInputElement[] {
    return Array.from(el(fixture).querySelectorAll<HTMLInputElement>('input[data-permission]'));
  }

  function pick(fixture: Fixture, roleId: number): void {
    (el(fixture).querySelector(`[data-role="${roleId}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function openModule(fixture: Fixture, name: string): void {
    (el(fixture).querySelector(`[data-module="${name}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function tick(fixture: Fixture, key: string, checked: boolean): void {
    const input = box(fixture, key)!;
    input.checked = checked;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function type(fixture: Fixture, value: string): void {
    const input = el(fixture).querySelector('[data-field="name"]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function search(fixture: Fixture, value: string): void {
    const input = el(fixture).querySelector('[data-field="search"]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function press(fixture: Fixture, action: string): void {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function dialogButton(fixture: Fixture, action: 'confirm' | 'cancel'): HTMLButtonElement {
    return el(fixture).querySelector(`tf-confirm-dialog [data-action="${action}"]`) as HTMLButtonElement;
  }

  function moduleNames(fixture: Fixture): string[] {
    return Array.from(el(fixture).querySelectorAll('[data-module]')).map((node) => node.getAttribute('data-module')!);
  }

  function saveButton(fixture: Fixture): HTMLButtonElement {
    return el(fixture).querySelector('[data-action="save"]') as HTMLButtonElement;
  }

  // ------------------------------------------------------------------ the role list

  it('lists every role and marks Administrator, Manager and Employee as system roles', () => {
    const fixture = create();

    const rows = Array.from(el(fixture).querySelectorAll('[data-role]')).map((row) => ({
      name: row.querySelector('.rc-role__name')?.textContent?.trim(),
      badge: row.querySelector('.tf-badge')?.textContent?.trim()
    }));

    expect(rows).toEqual([
      { name: 'Administrator', badge: 'System' },
      { name: 'Manager', badge: 'System' },
      { name: 'Employee', badge: 'System' },
      { name: 'Team Lead', badge: 'Custom' },
      { name: 'Busy Role', badge: 'Custom' }
    ]);
    expect(el(fixture).textContent).toContain('4 users');
  });

  it('opens on the first role', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-role="1"]')?.getAttribute('aria-current')).toBe('true');
  });

  it('finds a role by name', () => {
    const fixture = create();
    const input = el(fixture).querySelector('[data-field="role-search"]') as HTMLInputElement;

    input.value = 'lead';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(Array.from(el(fixture).querySelectorAll('[data-role]')).map((row) => row.getAttribute('data-role'))).toEqual(['4']);
  });

  it('shows a retryable error when the roles cannot be loaded', () => {
    listSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500, error: { error: 'Roles are unavailable.' } })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Roles are unavailable.');

    listSpy.and.returnValue(of(seedRoles()));
    press(fixture, 'retry');

    expect(el(fixture).querySelector('[data-role="1"]')).not.toBeNull();
  });

  // ------------------------------------------------------------------ one tree for every role

  it('shows the same modules for every role, with no role-specific copies', () => {
    const fixture = create();
    const expected = ['Dashboard', 'Tasks', 'Projects', 'Reports and workload', 'Mail', 'Role configuration'];

    for (const id of [1, 2, 3, 4, 5]) {
      pick(fixture, id);
      expect(moduleNames(fixture)).toEqual(expected);
    }
  });

  it('shows every dashboard permission in one place for the Employee role', () => {
    const fixture = create();
    pick(fixture, 3);
    openModule(fixture, 'Dashboard');

    const names = Array.from(el(fixture).querySelectorAll('[data-module-panel="Dashboard"] .rc-feature__name')).map((node) =>
      node.textContent?.trim().replace(/\s+/g, ' ')
    );

    expect(names.some((name) => name?.startsWith('View dashboard'))).toBeTrue();
    expect(names.some((name) => name?.startsWith('View charts and reports'))).toBeTrue();
    expect(box(fixture, 'dashboard.analytics.view')?.checked).toBeTrue();
  });

  // ------------------------------------------------------------------ checkboxes and counts

  it('ticks exactly the permissions the selected role holds and counts them', () => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Tasks');

    expect(box(fixture, 'tasks.create')?.checked).toBeTrue();
    expect(box(fixture, 'tasks.delete')?.checked).toBeFalse();
    // View my tasks, View my dashboard and Read mail are always on, so they count as enabled.
    expect(el(fixture).querySelector('[data-count]')?.textContent).toContain('5 of 15 permissions');
  });

  it('shows partial states for a screen with only some permissions on', () => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Tasks');

    const screen = el(fixture).querySelector('[data-screen-toggle="Tasks/Task list"]') as HTMLInputElement;
    const module = el(fixture).querySelector('[data-module-toggle="Tasks"]') as HTMLInputElement;

    expect(screen.indeterminate).toBeTrue();
    expect(module.indeterminate).toBeTrue();
  });

  // ------------------------------------------------------------------ system roles are view-only

  for (const [id, name] of [[1, 'Administrator'], [2, 'Manager'], [3, 'Employee']] as const) {
    it(`shows ${name} read-only: every checkbox disabled and no save, delete or rename control`, () => {
      const fixture = create();
      pick(fixture, id);

      for (const module of moduleNames(fixture)) {
        openModule(fixture, module);
        expect(boxes(fixture).every((input) => input.disabled)).toBeTrue();
      }

      expect(el(fixture).querySelector('[data-action="save"]')).toBeNull();
      expect(el(fixture).querySelector('[data-action="delete"]')).toBeNull();
      expect(el(fixture).querySelector('[data-field="name"]')).toBeNull();
      expect(el(fixture).querySelector('[data-note="read-only"]')?.textContent).toContain('read-only');
      expect(el(fixture).querySelector('[data-action="duplicate"]')).not.toBeNull();
    });
  }

  it('shows the Manager holding exactly its default permissions', () => {
    const fixture = create();
    pick(fixture, 2);

    const held = fixture.componentInstance.heldCount();

    // Eight stored permissions plus the three always-on features.
    expect(held).toBe(MANAGER_KEYS.length + 3);
    expect(fixture.componentInstance.isChecked('tasks.delete')).toBeFalse();
    expect(fixture.componentInstance.isChecked('projects.create')).toBeFalse();
  });

  it('shows the Administrator holding every permission, including role management', () => {
    const fixture = create();
    openModule(fixture, 'Role configuration');

    expect(box(fixture, 'roles.manage')?.checked).toBeTrue();
    expect(fixture.componentInstance.heldCount()).toBe(fixture.componentInstance.totalCount());
  });

  it('ignores a change event on a system role: nothing is ticked and nothing is saved', () => {
    const fixture = create();
    pick(fixture, 3);
    openModule(fixture, 'Tasks');

    // A user cannot click a disabled box; this forces the event to prove the component would not act on it anyway.
    tick(fixture, 'tasks.create', true);

    expect(fixture.componentInstance.isChecked('tasks.create')).toBeFalse();
    expect(fixture.componentInstance.draftKeys()).toEqual([]);
    expect(fixture.componentInstance.dirty()).toBeFalse();
    expect(updateSpy).not.toHaveBeenCalled();
  });

  it('starts a custom role from a system role without changing the system role', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 2);
    createSpy.and.returnValue(of(role(6, 'Manager copy', false, 0, ['tasks.create'])));

    press(fixture, 'duplicate');
    flushMicrotasks();
    fixture.detectChanges();

    expect((el(fixture).querySelector('[data-field="name"]') as HTMLInputElement).value).toBe('Manager copy');
    expect([...fixture.componentInstance.draftKeys()].sort()).toEqual([...MANAGER_KEYS].sort());

    openModule(fixture, 'Tasks');
    tick(fixture, 'tasks.create', false);
    press(fixture, 'save');

    const [request] = createSpy.calls.mostRecent().args;
    expect(request.name).toBe('Manager copy');
    expect(request.permissionKeys).not.toContain('tasks.create');
    expect(fixture.componentInstance.roles().find((candidate) => candidate.id === 2)?.permissionKeys).toEqual(MANAGER_KEYS);
  }));

  // ------------------------------------------------------------------ custom roles: edit and save

  it('shows a custom role editable, without the role-management permission to tick', () => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Tasks');

    const ticked = boxes(fixture).filter((input) => input.checked && !input.disabled).map((input) => input.dataset['permission']);

    expect(box(fixture, 'tasks.create')?.disabled).toBeFalse();
    expect(ticked).toContain('tasks.create');
    expect(el(fixture).querySelector('[data-module="Role configuration"]')).not.toBeNull();
    expect(fixture.componentInstance.totalCount()).toBe(CATALOG.length - 1);
    expect((el(fixture).querySelector('[data-field="name"]') as HTMLInputElement).value).toBe('Team Lead');
    expect(el(fixture).querySelector('[data-action="delete"]')).not.toBeNull();
  });

  it('shows features every user has as ticked and locked on a custom role', () => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Mail');

    expect(box(fixture, 'mail.read')?.checked).toBeTrue();
    expect(box(fixture, 'mail.read')?.disabled).toBeTrue();
    expect(el(fixture).textContent).toContain('Always on');
  });

  it('keeps Save disabled until something changes', () => {
    const fixture = create();
    pick(fixture, 4);

    expect(saveButton(fixture).disabled).toBeTrue();

    openModule(fixture, 'Reports and workload');
    tick(fixture, 'workload.view', true);

    expect(saveButton(fixture).disabled).toBeFalse();
    expect(el(fixture).querySelector('.rc-savebar__text')?.textContent).toContain('1 unsaved change');
  });

  it('saves the edited permissions and name, shows the saved state and confirms', () => {
    const fixture = create();
    pick(fixture, 4);
    updateSpy.and.returnValue(of(role(4, 'Team Leader', false, 0, ['tasks.create', 'workload.view'])));

    type(fixture, 'Team Leader');
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'reports.view', false);
    tick(fixture, 'workload.view', true);
    press(fixture, 'save');

    expect(updateSpy).toHaveBeenCalledTimes(1);
    const [id, request] = updateSpy.calls.mostRecent().args;
    expect(id).toBe(4);
    expect(request.name).toBe('Team Leader');
    expect([...request.permissionKeys].sort()).toEqual(['tasks.create', 'workload.view']);
    expect(toast.success).toHaveBeenCalledWith('Team Leader saved');
    expect(el(fixture).querySelector('[data-role="4"] .rc-role__name')?.textContent?.trim()).toBe('Team Leader');
    expect(box(fixture, 'workload.view')?.checked).toBeTrue();
    expect(box(fixture, 'reports.view')?.checked).toBeFalse();
  });

  it('shows the reason when a save is rejected and keeps the edits', () => {
    const fixture = create();
    pick(fixture, 4);
    updateSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'A role named "Manager" already exists.' } }))
    );

    type(fixture, 'Manager');
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'workload.view', true);
    press(fixture, 'save');

    expect(el(fixture).querySelector('[data-error="form"]')?.textContent).toContain('already exists');
    expect((el(fixture).querySelector('[data-field="name"]') as HTMLInputElement).value).toBe('Manager');
    expect(box(fixture, 'workload.view')?.checked).toBeTrue();
    expect(toast.success).not.toHaveBeenCalled();
  });

  // ------------------------------------------------------------------ dependencies, scope and bulk selection

  it('ticks the View permission an action needs, and clears the actions when View is cleared', () => {
    const fixture = create();
    pick(fixture, 5);
    openModule(fixture, 'Projects');

    tick(fixture, 'projects.create', true);
    expect(box(fixture, 'projects.view')?.checked).toBeTrue();

    tick(fixture, 'projects.view', false);
    expect(box(fixture, 'projects.create')?.checked).toBeFalse();
  });

  it('picks a dashboard scope: a wider scope includes the narrower one', () => {
    const fixture = create();
    pick(fixture, 5);
    openModule(fixture, 'Dashboard');
    const level = (key: string) => el(fixture).querySelector(`button[data-permission="${key}"]`) as HTMLButtonElement;

    expect(level('dashboard.view.own').getAttribute('aria-checked')).toBe('true');

    level('dashboard.view.organization').click();
    fixture.detectChanges();

    expect(level('dashboard.view.organization').getAttribute('aria-checked')).toBe('true');
    expect(fixture.componentInstance.isChecked('dashboard.view.team')).toBeTrue();

    level('dashboard.view.team').click();
    fixture.detectChanges();

    expect(fixture.componentInstance.draftKeys()).toContain('dashboard.view.team');
    expect(fixture.componentInstance.draftKeys()).not.toContain('dashboard.view.organization');

    level('dashboard.view.own').click();
    fixture.detectChanges();

    expect(fixture.componentInstance.draftKeys().filter((key) => key.startsWith('dashboard.view'))).toEqual([]);
  });

  it('turns a whole module on and off with its checkbox', () => {
    const fixture = create();
    pick(fixture, 5);
    openModule(fixture, 'Projects');
    const toggle = el(fixture).querySelector('[data-module-toggle="Projects"]') as HTMLInputElement;

    toggle.checked = true;
    toggle.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(['projects.view', 'projects.create', 'projects.delete'].every((key) => fixture.componentInstance.isChecked(key))).toBeTrue();
    expect(toggle.checked).toBeTrue();

    toggle.checked = false;
    toggle.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(['projects.view', 'projects.create', 'projects.delete'].some((key) => fixture.componentInstance.isChecked(key))).toBeFalse();
  });

  it('turns a screen on with its checkbox', () => {
    const fixture = create();
    pick(fixture, 5);
    openModule(fixture, 'Reports and workload');
    const toggle = el(fixture).querySelector('[data-screen-toggle="Reports and workload/Reports"]') as HTMLInputElement;

    toggle.checked = true;
    toggle.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(fixture.componentInstance.isChecked('reports.view')).toBeTrue();
    expect(fixture.componentInstance.isChecked('reports.export')).toBeTrue();
  });

  it('selects and deselects everything a custom role may hold', () => {
    const fixture = create();
    pick(fixture, 5);

    press(fixture, 'select-all');
    expect([...fixture.componentInstance.draftKeys()].sort()).toEqual([...SELECTABLE].sort());
    expect(fixture.componentInstance.heldCount()).toBe(fixture.componentInstance.totalCount());

    press(fixture, 'deselect-all');
    expect(fixture.componentInstance.draftKeys()).toEqual([]);
    expect(fixture.componentInstance.heldCount()).toBe(3);
  });

  it('never offers Select all on a system role', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-action="select-all"]')).toBeNull();
  });

  // ------------------------------------------------------------------ search, filter and view

  it('searches every module for matching permissions', () => {
    const fixture = create();
    pick(fixture, 5);

    search(fixture, 'delete');

    const keys = boxes(fixture).map((input) => input.dataset['permission']);
    expect(keys).toEqual(['tasks.delete', 'projects.delete']);
    expect(el(fixture).querySelector('.rc-panel__hint')?.textContent).toContain('2 permissions match');
    expect(el(fixture).querySelector('[data-count]')?.textContent).toContain('of 15 permissions');
  });

  it('filters to the permissions that are enabled or disabled', () => {
    const fixture = create();
    pick(fixture, 4);

    (el(fixture).querySelector('[data-filter="enabled"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const enabled = boxes(fixture).map((input) => input.dataset['permission']);

    (el(fixture).querySelector('[data-filter="disabled"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const disabled = boxes(fixture).map((input) => input.dataset['permission']);

    expect(enabled).toContain('tasks.create');
    expect(enabled).not.toContain('tasks.delete');
    expect(disabled).toContain('tasks.delete');
    expect(disabled).not.toContain('tasks.create');
  });

  it('says so when nothing matches the search', () => {
    const fixture = create();

    search(fixture, 'zzzz');

    expect(el(fixture).textContent).toContain('No permissions match');
  });

  it('collapses and expands screens', () => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Tasks');

    press(fixture, 'collapse-all');
    expect(boxes(fixture).length).toBe(0);

    press(fixture, 'expand-all');
    expect(boxes(fixture).length).toBeGreaterThan(0);
  });

  it('compares every role side by side', () => {
    const fixture = create();

    (el(fixture).querySelector('[data-view="matrix"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const headers = Array.from(el(fixture).querySelectorAll('.rc-matrix thead th')).map((cell) => cell.textContent?.trim());
    expect(headers).toEqual(['Permission', 'Administrator', 'Manager', 'Employee', 'Team Lead', 'Busy Role']);

    const row = Array.from(el(fixture).querySelectorAll('.rc-matrix tbody tr')).find((candidate) =>
      candidate.textContent?.includes('Create tasks')
    )!;
    const cells = Array.from(row.querySelectorAll('td')).map((cell) => cell.querySelector('.rc-dot--on') !== null);
    expect(cells).toEqual([true, true, false, true, false]);
  });

  // ------------------------------------------------------------------ copy from another role

  it('copies the permissions of another role into the draft', () => {
    const fixture = create();
    pick(fixture, 5);

    fixture.componentInstance.copyFrom(2);
    fixture.detectChanges();

    expect([...fixture.componentInstance.draftKeys()].sort()).toEqual([...MANAGER_KEYS].sort());
    expect(fixture.componentInstance.dirty()).toBeTrue();
  });

  // ------------------------------------------------------------------ new role

  it('creates a role from a name and ticked permissions and selects it', () => {
    const fixture = create();
    createSpy.and.returnValue(of(role(6, 'Auditor', false, 0, ['reports.view'])));

    press(fixture, 'new-role');
    type(fixture, '  Auditor  ');
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'reports.view', true);
    press(fixture, 'save');

    expect(createSpy).toHaveBeenCalledWith({ name: 'Auditor', permissionKeys: ['reports.view'] });
    expect(toast.success).toHaveBeenCalledWith('Auditor created');
    expect(el(fixture).querySelector('[data-role="6"]')?.getAttribute('aria-current')).toBe('true');
    expect(el(fixture).querySelector('[data-role="6"] .tf-badge')?.textContent?.trim()).toBe('Custom');
  });

  it('does not create a role without a name', () => {
    const fixture = create();

    press(fixture, 'new-role');
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'reports.view', true);
    press(fixture, 'save');

    expect(createSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="name"]')?.textContent).toContain('Enter a name');
  });

  it('never offers the role-management permission on a new role', () => {
    const fixture = create();

    press(fixture, 'new-role');
    openModule(fixture, 'Role configuration');

    expect(box(fixture, 'roles.manage')).toBeNull();
  });

  // ------------------------------------------------------------------ unsaved edits

  it('does not apply unsaved edits: discarding them restores the stored role', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'workload.view', true);

    pick(fixture, 2);
    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeTrue();
    dialogButton(fixture, 'confirm').click();
    flushMicrotasks();
    fixture.detectChanges();
    pick(fixture, 4);
    openModule(fixture, 'Reports and workload');

    expect(updateSpy).not.toHaveBeenCalled();
    expect(box(fixture, 'workload.view')?.checked).toBeFalse();
    expect(box(fixture, 'reports.view')?.checked).toBeTrue();
  }));

  it('stays on the role with unsaved edits when leaving is cancelled', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'workload.view', true);

    pick(fixture, 2);
    dialogButton(fixture, 'cancel').click();
    flushMicrotasks();
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-role="4"]')?.getAttribute('aria-current')).toBe('true');
    expect(box(fixture, 'workload.view')?.checked).toBeTrue();
  }));

  it('discards edits from the save bar after confirmation', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);
    openModule(fixture, 'Reports and workload');
    tick(fixture, 'workload.view', true);

    press(fixture, 'discard');
    dialogButton(fixture, 'confirm').click();
    flushMicrotasks();
    fixture.detectChanges();

    expect(fixture.componentInstance.dirty()).toBeFalse();
    expect(fixture.componentInstance.draftKeys().sort()).toEqual(['reports.view', 'tasks.create']);
  }));

  it('switches roles without asking when nothing was changed', () => {
    const fixture = create();
    pick(fixture, 4);

    pick(fixture, 5);

    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeFalse();
    expect(el(fixture).querySelector('[data-role="5"]')?.getAttribute('aria-current')).toBe('true');
  });

  // ------------------------------------------------------------------ narrow screens

  it('shows the role list first on narrow screens and the permissions after a role is chosen', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.rc-layout--detail')).toBeNull();

    pick(fixture, 4);
    expect(el(fixture).querySelector('.rc-layout--detail')).not.toBeNull();

    press(fixture, 'back-to-roles');
    expect(el(fixture).querySelector('.rc-layout--detail')).toBeNull();
  });

  // ------------------------------------------------------------------ delete

  it('deletes an unused custom role after confirmation', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);
    deleteSpy.and.returnValue(of(undefined));

    press(fixture, 'delete');
    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeTrue();
    expect(el(fixture).querySelector('tf-confirm-dialog')?.textContent).toContain('Delete Team Lead?');
    dialogButton(fixture, 'confirm').click();
    flushMicrotasks();
    fixture.detectChanges();

    expect(deleteSpy).toHaveBeenCalledWith(4);
    expect(toast.success).toHaveBeenCalledWith('Team Lead deleted');
    expect(el(fixture).querySelector('[data-role="4"]')).toBeNull();
  }));

  it('does not delete when the confirmation is cancelled', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);

    press(fixture, 'delete');
    dialogButton(fixture, 'cancel').click();
    flushMicrotasks();

    expect(deleteSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-role="4"]')).not.toBeNull();
  }));

  it('explains why a role in use could not be deleted and keeps it', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 5);
    deleteSpy.and.returnValue(
      throwError(
        () => new HttpErrorResponse({ status: 409, error: { error: '2 users still hold this role. Move them to another role first.' } })
      )
    );

    press(fixture, 'delete');
    dialogButton(fixture, 'confirm').click();
    flushMicrotasks();
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-error="form"]')?.textContent).toContain('2 users still hold this role');
    expect(el(fixture).querySelector('[data-role="5"]')).not.toBeNull();
    expect(toast.success).not.toHaveBeenCalled();
  }));
});
