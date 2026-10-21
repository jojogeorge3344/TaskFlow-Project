import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Permissions } from '../../core/auth/permissions';
import { AccessRole, PermissionInfo } from '../../core/models';
import { RoleService } from '../../core/services/role.service';
import { ADMINISTRATOR_PERMISSIONS, systemPermissions } from '../../testing/permissions';
import { ToastService } from '../../shared/ui/toast.service';
import { PermissionsManagementComponent } from './permissions-management.component';

const GROUPS: Record<string, string> = {
  [Permissions.DashboardAdmin]: 'Dashboards',
  [Permissions.DashboardManager]: 'Dashboards',
  [Permissions.DepartmentsView]: 'Departments',
  [Permissions.DepartmentsManage]: 'Departments',
  [Permissions.EmployeesView]: 'Employees',
  [Permissions.EmployeesManage]: 'Employees',
  [Permissions.ProjectsView]: 'Projects',
  [Permissions.ProjectsManage]: 'Projects',
  [Permissions.TasksViewAll]: 'Tasks',
  [Permissions.TasksCreate]: 'Tasks',
  [Permissions.TasksUpdate]: 'Tasks',
  [Permissions.TasksReassign]: 'Tasks',
  [Permissions.TasksRecipients]: 'Tasks',
  [Permissions.ReportsView]: 'Reports and workload',
  [Permissions.WorkloadView]: 'Reports and workload',
  [Permissions.SettingsReminders]: 'Settings',
  [Permissions.SettingsApplication]: 'Settings',
  [Permissions.SettingsAi]: 'Settings',
  [Permissions.RolesManage]: 'Access control'
};

const CATALOG: PermissionInfo[] = ADMINISTRATOR_PERMISSIONS.map((key, index) => ({
  id: index + 1,
  key,
  name: `Name of ${key}`,
  description: `Lets a role use ${key}.`,
  group: GROUPS[key],
  isAssignable: key !== Permissions.RolesManage
}));

function role(id: number, name: string, isSystem: boolean, userCount: number, permissionKeys: string[]): AccessRole {
  return { id, name, isSystem, userCount, permissionKeys };
}

function seedRoles(): AccessRole[] {
  return [
    role(1, 'Administrator', true, 1, systemPermissions('Administrator')),
    role(2, 'Manager', true, 1, systemPermissions('Manager')),
    role(3, 'Employee', true, 4, []),
    role(4, 'Team Lead', false, 0, [Permissions.TasksCreate, Permissions.ReportsView]),
    role(5, 'Busy Role', false, 2, [Permissions.WorkloadView])
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

  function el(fixture: ComponentFixture<PermissionsManagementComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function box(fixture: ComponentFixture<PermissionsManagementComponent>, key: string): HTMLInputElement | null {
    return el(fixture).querySelector(`[data-permission="${key}"]`);
  }

  function boxes(fixture: ComponentFixture<PermissionsManagementComponent>): HTMLInputElement[] {
    return Array.from(el(fixture).querySelectorAll<HTMLInputElement>('[data-permission]'));
  }

  function pick(fixture: ComponentFixture<PermissionsManagementComponent>, roleId: number): void {
    (el(fixture).querySelector(`[data-role="${roleId}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function tick(fixture: ComponentFixture<PermissionsManagementComponent>, key: string, checked: boolean): void {
    const input = box(fixture, key)!;
    input.checked = checked;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function type(fixture: ComponentFixture<PermissionsManagementComponent>, value: string): void {
    const input = el(fixture).querySelector('[data-field="name"]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function press(fixture: ComponentFixture<PermissionsManagementComponent>, action: string): void {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function dialogButton(fixture: ComponentFixture<PermissionsManagementComponent>, action: 'confirm' | 'cancel'): HTMLButtonElement {
    return el(fixture).querySelector(`tf-confirm-dialog [data-action="${action}"]`) as HTMLButtonElement;
  }

  // ------------------------------------------------------------------ the role list

  it('lists every role and marks Administrator, Manager and Employee as system roles', () => {
    const fixture = create();

    const rows = Array.from(el(fixture).querySelectorAll('[data-role]')).map((row) => ({
      name: row.querySelector('.perm-role__name')?.textContent?.trim(),
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

  it('shows a retryable error when the roles cannot be loaded', () => {
    listSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500, error: { error: 'Roles are unavailable.' } })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Roles are unavailable.');

    listSpy.and.returnValue(of(seedRoles()));
    press(fixture, 'retry');

    expect(el(fixture).querySelector('[data-role="1"]')).not.toBeNull();
  });

  // ------------------------------------------------------------------ checkboxes

  it('ticks exactly the permissions the selected role holds', () => {
    const fixture = create();
    pick(fixture, 2);

    const ticked = boxes(fixture).filter((input) => input.checked).map((input) => input.dataset['permission']);

    expect(ticked.sort()).toEqual([...systemPermissions('Manager')].sort());
    expect(box(fixture, Permissions.RolesManage)?.checked).toBeFalse();
  });

  // ------------------------------------------------------------------ system roles are view-only

  for (const [id, name] of [[1, 'Administrator'], [2, 'Manager'], [3, 'Employee']] as const) {
    it(`shows ${name} read-only: every checkbox disabled and no save, delete or rename control`, () => {
      const fixture = create();
      pick(fixture, id);

      expect(boxes(fixture).length).toBe(CATALOG.length);
      expect(boxes(fixture).every((input) => input.disabled)).toBeTrue();
      expect(el(fixture).querySelector('[data-action="save"]')).toBeNull();
      expect(el(fixture).querySelector('[data-action="delete"]')).toBeNull();
      expect(el(fixture).querySelector('[data-field="name"]')).toBeNull();
      expect(el(fixture).querySelector('[data-note="read-only"]')?.textContent).toContain('read-only');
    });
  }

  it('shows the Administrator holding every permission, including role management', () => {
    const fixture = create();

    expect(boxes(fixture).every((input) => input.checked)).toBeTrue();
    expect(box(fixture, Permissions.RolesManage)?.checked).toBeTrue();
  });

  it('ignores a change event on a system role: nothing is ticked and nothing is saved', () => {
    const fixture = create();
    pick(fixture, 3);

    // A user cannot click a disabled box; this forces the event to prove the component would not act on it anyway.
    tick(fixture, Permissions.TasksCreate, true);

    expect(fixture.componentInstance.isChecked(Permissions.TasksCreate)).toBeFalse();
    expect(fixture.componentInstance.draftKeys()).toEqual([]);
    expect(fixture.componentInstance.dirty()).toBeFalse();
    expect(el(fixture).querySelector('[data-action="save"]')).toBeNull();
    expect(updateSpy).not.toHaveBeenCalled();
  });

  // ------------------------------------------------------------------ custom roles: edit and save

  it('shows a custom role editable, without the role-management permission to tick', () => {
    const fixture = create();
    pick(fixture, 4);

    const ticked = boxes(fixture).filter((input) => input.checked).map((input) => input.dataset['permission']);

    expect(boxes(fixture).every((input) => !input.disabled)).toBeTrue();
    expect(ticked.sort()).toEqual([Permissions.ReportsView, Permissions.TasksCreate]);
    expect(box(fixture, Permissions.RolesManage)).toBeNull();
    expect((el(fixture).querySelector('[data-field="name"]') as HTMLInputElement).value).toBe('Team Lead');
    expect(el(fixture).querySelector('[data-action="delete"]')).not.toBeNull();
  });

  it('keeps Save disabled until something changes', () => {
    const fixture = create();
    pick(fixture, 4);

    expect((el(fixture).querySelector('[data-action="save"]') as HTMLButtonElement).disabled).toBeTrue();

    tick(fixture, Permissions.WorkloadView, true);

    expect((el(fixture).querySelector('[data-action="save"]') as HTMLButtonElement).disabled).toBeFalse();
  });

  it('saves the edited permissions and name, shows the saved state and confirms', () => {
    const fixture = create();
    pick(fixture, 4);
    updateSpy.and.returnValue(
      of(role(4, 'Team Leader', false, 0, [Permissions.TasksCreate, Permissions.WorkloadView]))
    );

    type(fixture, 'Team Leader');
    tick(fixture, Permissions.ReportsView, false);
    tick(fixture, Permissions.WorkloadView, true);
    press(fixture, 'save');

    expect(updateSpy).toHaveBeenCalledTimes(1);
    const [id, request] = updateSpy.calls.mostRecent().args;
    expect(id).toBe(4);
    expect(request.name).toBe('Team Leader');
    expect([...request.permissionKeys].sort()).toEqual([Permissions.TasksCreate, Permissions.WorkloadView]);
    expect(toast.success).toHaveBeenCalledWith('Team Leader saved');
    expect(el(fixture).querySelector('[data-role="4"] .perm-role__name')?.textContent?.trim()).toBe('Team Leader');
    expect(box(fixture, Permissions.WorkloadView)?.checked).toBeTrue();
    expect(box(fixture, Permissions.ReportsView)?.checked).toBeFalse();
  });

  it('shows the reason when a save is rejected and keeps the edits', () => {
    const fixture = create();
    pick(fixture, 4);
    updateSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { error: 'A role named "Manager" already exists.' } }))
    );

    type(fixture, 'Manager');
    tick(fixture, Permissions.WorkloadView, true);
    press(fixture, 'save');

    expect(el(fixture).querySelector('[data-error="form"]')?.textContent).toContain('already exists');
    expect((el(fixture).querySelector('[data-field="name"]') as HTMLInputElement).value).toBe('Manager');
    expect(box(fixture, Permissions.WorkloadView)?.checked).toBeTrue();
    expect(toast.success).not.toHaveBeenCalled();
  });

  // ------------------------------------------------------------------ new role

  it('creates a role from a name and ticked permissions and selects it', () => {
    const fixture = create();
    createSpy.and.returnValue(of(role(6, 'Auditor', false, 0, [Permissions.ReportsView])));

    press(fixture, 'new-role');
    (fixture.nativeElement as HTMLElement).querySelector('[data-role="new"]');
    type(fixture, '  Auditor  ');
    tick(fixture, Permissions.ReportsView, true);
    press(fixture, 'save');

    expect(createSpy).toHaveBeenCalledWith({ name: 'Auditor', permissionKeys: [Permissions.ReportsView] });
    expect(toast.success).toHaveBeenCalledWith('Auditor created');
    expect(el(fixture).querySelector('[data-role="6"]')?.getAttribute('aria-current')).toBe('true');
    expect(el(fixture).querySelector('[data-role="6"] .tf-badge')?.textContent?.trim()).toBe('Custom');
  });

  it('does not create a role without a name', () => {
    const fixture = create();

    press(fixture, 'new-role');
    tick(fixture, Permissions.ReportsView, true);
    press(fixture, 'save');

    expect(createSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="name"]')?.textContent).toContain('Enter a name');
  });

  it('never offers the role-management permission on a new role', () => {
    const fixture = create();

    press(fixture, 'new-role');

    expect(box(fixture, Permissions.RolesManage)).toBeNull();
    expect(boxes(fixture).length).toBe(CATALOG.length - 1);
  });

  // ------------------------------------------------------------------ unsaved edits

  it('does not apply unsaved edits: discarding them restores the stored role', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);
    tick(fixture, Permissions.WorkloadView, true);

    pick(fixture, 2);
    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeTrue();
    dialogButton(fixture, 'confirm').click();
    flushMicrotasks();
    fixture.detectChanges();
    pick(fixture, 4);

    expect(updateSpy).not.toHaveBeenCalled();
    expect(box(fixture, Permissions.WorkloadView)?.checked).toBeFalse();
    expect(box(fixture, Permissions.TasksCreate)?.checked).toBeTrue();
  }));

  it('stays on the role with unsaved edits when leaving is cancelled', fakeAsync(() => {
    const fixture = create();
    pick(fixture, 4);
    tick(fixture, Permissions.WorkloadView, true);

    pick(fixture, 2);
    dialogButton(fixture, 'cancel').click();
    flushMicrotasks();
    fixture.detectChanges();

    expect(el(fixture).querySelector('[data-role="4"]')?.getAttribute('aria-current')).toBe('true');
    expect(box(fixture, Permissions.WorkloadView)?.checked).toBeTrue();
  }));

  it('switches roles without asking when nothing was changed', () => {
    const fixture = create();
    pick(fixture, 4);

    pick(fixture, 5);

    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeFalse();
    expect(el(fixture).querySelector('[data-role="5"]')?.getAttribute('aria-current')).toBe('true');
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
