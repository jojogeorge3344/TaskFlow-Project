import { HttpErrorResponse } from '@angular/common/http';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { DepartmentWithCount, Position, User } from '../../core/models';
import { PositionService } from '../../core/services/position.service';
import { withProject } from '../../testing/users';
import { Permissions } from '../../core/auth/permissions';
import { RoleService } from '../../core/services/role.service';
import { UserService } from '../../core/services/user.service';
import { ToastService } from '../../shared/ui/toast.service';
import { EmployeeDetailsPanelComponent } from './employee-details-panel.component';
import { EmployeeFormPanelComponent } from './employee-form-panel.component';

const existing: User = {
  id: 7,
  employeeCode: 'EMP-022',
  fullName: 'Rahul Menon',
  username: 'rahul.m',
  email: 'rahul.menon@taskflow.com',
  role: 'Employee',
  roleId: 3,
  isActive: true,
  createdAt: '2026-09-05T00:00:00Z',
  lastLoginAt: null,
  ...withProject(1, 'Engineering'),
  departments: [],
  managers: []
};

const positions: Position[] = [
  { id: 1, name: 'Member' },
  { id: 3, name: 'QA' }
];

const manager: User = {
  ...existing,
  id: 20,
  fullName: 'Anupama Nair',
  username: 'anupama.n',
  email: 'anupama.nair@taskflow.com',
  role: 'Manager',
  roleId: 2,
  managerProjects: [],
  reports: []
};

const departments: DepartmentWithCount[] = [
  { id: 5, name: 'Human Resources', description: null, createdAt: '2026-09-01', managerCount: 0, employeeCount: 2 },
  { id: 6, name: 'Finance', description: null, createdAt: '2026-09-01', managerCount: 1, employeeCount: 1 }
];

describe('EmployeeFormPanelComponent', () => {
  let createSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let getAllSpy: jasmine.Spy;
  let errorToastSpy: jasmine.Spy;
  let toastSpy: jasmine.Spy;
  let clearSessionSpy: jasmine.Spy;
  let navigateSpy: jasmine.Spy;
  const currentUser = signal<AuthUser | null>(null);

  function create(employee: User | null): ComponentFixture<EmployeeFormPanelComponent> {
    const fixture = TestBed.createComponent(EmployeeFormPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.componentRef.setInput('projects', [
      { id: 1, name: 'Engineering', description: null, createdAt: '2026-09-01', employeeCount: 1 },
      { id: 2, name: 'Sales', description: null, createdAt: '2026-09-01', employeeCount: 0 }
    ]);
    fixture.componentRef.setInput('departments', departments);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<EmployeeFormPanelComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function type(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: string, value: string): void {
    const input = el(fixture).querySelector(`#${id}`) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function submit(fixture: ComponentFixture<EmployeeFormPanelComponent>): void {
    (el(fixture).querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    createSpy = jasmine.createSpy('create');
    updateSpy = jasmine.createSpy('update');
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of([manager]));

    currentUser.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.com', role: 'Administrator', employeeCode: null, permissions: systemPermissions('Administrator') });
    clearSessionSpy = jasmine.createSpy('clearSession');

    await TestBed.configureTestingModule({
      imports: [EmployeeFormPanelComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: { create: createSpy, update: updateSpy, getAll: getAllSpy } },
        { provide: PositionService, useValue: { getAll: () => of(positions), getOrCreate: jasmine.createSpy('getOrCreate') } },
        { provide: AuthService, useValue: fakeAuthService(currentUser.asReadonly(), { clearSession: clearSessionSpy }) },
        {
          provide: RoleService,
          useValue: {
            assignable: () =>
              of([
                { id: 3, name: 'Employee', isSystem: true, userCount: 1, permissionKeys: [] },
                { id: 2, name: 'Manager', isSystem: true, userCount: 1, permissionKeys: [] },
                { id: 1, name: 'Administrator', isSystem: true, userCount: 1, permissionKeys: [] },
                { id: 9, name: 'Team Lead', isSystem: false, userCount: 0, permissionKeys: [] }
              ])
          }
        }
      ]
    }).compileComponents();

    toastSpy = spyOn(TestBed.inject(ToastService), 'success');
    errorToastSpy = spyOn(TestBed.inject(ToastService), 'error');
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
  });

  it('lists the assignable roles without a failure hint for a user who can assign roles but not manage them', () => {
    currentUser.set({
      id: 2, fullName: 'Assigner', username: 'assigner', email: 'assigner@taskflow.com', role: 'Team Lead', employeeCode: null,
      permissions: [Permissions.EmployeesView, Permissions.EmployeesEdit, Permissions.EmployeesRoleAssign]
    });
    const fixture = create(existing);

    expect(fixture.componentInstance.roleLoadFailed()).toBeFalse();
    expect(fixture.componentInstance.roles().map((role) => role.name)).toContain('Team Lead');
    expect(el(fixture).textContent).not.toContain('Custom roles could not be loaded');
  });

  it('keeps the current password when the new password is left blank', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(of({ ...existing, fullName: 'Rahul M' }));

    type(fixture, 'emp-name', 'Rahul M');
    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ password: null }));
    expect(toastSpy).toHaveBeenCalledWith('Rahul M updated');
  });

  it('has no password or status field when editing and sends no password', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(of(existing));

    expect(el(fixture).querySelector('#emp-password')).toBeNull();
    expect(el(fixture).querySelector('#emp-status')).toBeNull();
    expect(el(fixture).querySelector('#emp-sec-security')).toBeNull();
    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ password: null }));
    expect(toastSpy).toHaveBeenCalledWith('Rahul Menon updated');
  });

  it('signs the admin out at once when they change their own role', () => {
    const self: User = { ...existing, id: 1, username: 'admin', fullName: 'Admin', role: 'Administrator' };
    const fixture = create(self);
    updateSpy.and.returnValue(of({ ...self, role: 'Manager', roleId: 2 }));

    const role = el(fixture).querySelector('#emp-role') as HTMLSelectElement;
    role.value = Array.from(role.options).find((option) => option.text.trim() === 'Manager')!.value;
    role.dispatchEvent(new Event('change'));
    submit(fixture);

    expect(clearSessionSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'credentials-changed' } });
  });

  it('signs the admin out at once when they change their own username', () => {
    const self: User = { ...existing, id: 1, username: 'admin', fullName: 'Admin' };
    const fixture = create(self);
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    updateSpy.and.returnValue(of({ ...self, username: 'admin2' }));

    type(fixture, 'emp-username', 'admin2');
    submit(fixture);

    expect(clearSessionSpy).toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'credentials-changed' } });
    expect(savedSpy).not.toHaveBeenCalled();
  });

  it('shows employee code, password and status fields when adding', () => {
    const fixture = create(null);

    expect(el(fixture).querySelector('#emp-code')).toBeTruthy();
    expect(el(fixture).querySelector('#emp-password')).toBeTruthy();
    expect(el(fixture).querySelector('#emp-status')).toBeTruthy();
  });

  it('pre-fills the employee and warns that a username or role change signs them out', () => {
    const fixture = create(existing);

    expect((el(fixture).querySelector('#emp-name') as HTMLInputElement).value).toBe('Rahul Menon');
    expect((el(fixture).querySelector('#emp-code') as HTMLInputElement).value).toBe('EMP-022');
    expect(el(fixture).querySelector('#emp-signout-notice')?.textContent).toContain('signs this employee out');
  });

  it('shows inline required and email errors on submit without saving', () => {
    const fixture = create(null);
    type(fixture, 'emp-email', 'bad!name');

    submit(fixture);

    const errors = Array.from(el(fixture).querySelectorAll('.tf-field__error')).map((node) => node.textContent?.trim());
    expect(errors).toContain('Full name is required.');
    expect(errors).toContain('Username is required.');
    expect(errors).toContain('Use letters, digits, dot, underscore or hyphen before @taskflow.com.');
    expect(errors).toContain('Password is required.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('shows the fixed domain beside the email input and edits only the local part', () => {
    const fixture = create(existing);

    expect(el(fixture).querySelector('[data-email-suffix]')?.textContent?.trim()).toBe('@taskflow.com');
    expect((el(fixture).querySelector('#emp-email') as HTMLInputElement).value).toBe('rahul.menon');
  });

  describe('department membership', () => {
    function box(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: number): HTMLInputElement {
      return el(fixture).querySelector(`[data-department="${id}"]`) as HTMLInputElement;
    }

    function tick(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: number, checked: boolean): void {
      box(fixture, id).checked = checked;
      box(fixture, id).dispatchEvent(new Event('change'));
      fixture.detectChanges();
    }

    it('offers every department as a checkbox so a person can be in several', () => {
      const fixture = create(null);

      expect(el(fixture).querySelectorAll('[data-department]').length).toBe(2);
      expect(el(fixture).querySelector('#emp-department')).toBeNull();
    });

    it('includes every ticked department in the create payload', () => {
      const fixture = create(null);
      createSpy.and.returnValue(of({ ...existing, id: 41, fullName: 'Nia New' }));

      type(fixture, 'emp-name', 'Nia New');
      type(fixture, 'emp-username', 'nia.n');
      type(fixture, 'emp-email', 'nia');
      type(fixture, 'emp-password', 'Temp#1234');
      tick(fixture, 5, true);
      tick(fixture, 6, true);
      submit(fixture);

      expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({ departmentIds: [5, 6] }));
    });

    it('shows the departments of the loaded user read-only and keeps them when saving', () => {
      const fixture = create({ ...existing, departments: [{ id: 6, name: 'Finance' }] });
      updateSpy.and.returnValue(of(existing));

      const view = el(fixture).querySelector('[data-view="departments"]') as HTMLElement;
      expect(view.textContent).toContain('Finance');
      expect(view.textContent).not.toContain('Department management');
      expect(el(fixture).querySelectorAll('[data-department]').length).toBe(0);
      submit(fixture);

      expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ departmentIds: [6] }));
    });

    it('says so when the loaded user is in no department', () => {
      const fixture = create(existing);

      expect(el(fixture).querySelector('[data-view="departments"]')?.textContent).toContain('Not in any department');
    });

    it('shows the derived managers read-only and offers no manager field', () => {
      const fixture = create({ ...existing, managers: [{ id: 20, fullName: 'Anupama Nair' }] });

      expect(el(fixture).querySelector('[data-list="managers"]')?.textContent).toContain('Anupama Nair');
      expect(el(fixture).querySelector('#emp-manager')).toBeNull();
    });
  });

  describe('project allocations', () => {
    function fill(fixture: ComponentFixture<EmployeeFormPanelComponent>): void {
      type(fixture, 'emp-name', 'Nia New');
      type(fixture, 'emp-username', 'nia.n');
      type(fixture, 'emp-email', 'nia');
      type(fixture, 'emp-password', 'Temp#1234');
    }

    function summary(fixture: ComponentFixture<EmployeeFormPanelComponent>): string {
      return (el(fixture).querySelector('[data-allocation-summary]')?.textContent ?? '').replace(/\s+/g, ' ').trim();
    }

    function addRow(fixture: ComponentFixture<EmployeeFormPanelComponent>): void {
      (el(fixture).querySelector('[data-action="add-project"]') as HTMLButtonElement).click();
      fixture.detectChanges();
    }

    function setPercent(fixture: ComponentFixture<EmployeeFormPanelComponent>, row: number, value: string): void {
      const input = el(fixture).querySelector(`[data-row="${row}"] .allocation-row__percent`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    }

    function saveButton(fixture: ComponentFixture<EmployeeFormPanelComponent>): HTMLButtonElement {
      return el(fixture).querySelector('button[type="submit"]') as HTMLButtonElement;
    }

    it('starts with nothing allocated when adding', () => {
      const fixture = create(null);

      expect(el(fixture).querySelectorAll('[data-row]').length).toBe(0);
      expect(summary(fixture)).toBe('0% allocated, 100% remaining');
    });

    it('starts a new row on a free project with the remaining percentage and the Member position', () => {
      const fixture = create(null);

      addRow(fixture);

      expect(el(fixture).querySelectorAll('[data-row]').length).toBe(1);
      expect(summary(fixture)).toBe('100% allocated, 0% remaining');
      const first = el(fixture).querySelector('[data-row="0"] select') as HTMLSelectElement;
      expect(first.selectedOptions[0].text.trim()).toBe('Engineering');
      expect((el(fixture).querySelector('[data-row="0"] app-position-combobox input') as HTMLInputElement).value).toBe('Member');
    });

    it('flags a total above 100, disables Save and sends nothing', () => {
      const fixture = create(null);
      fill(fixture);
      addRow(fixture);
      addRow(fixture);

      setPercent(fixture, 1, '50');

      expect(summary(fixture)).toBe('150% allocated, 0% remaining');
      expect(el(fixture).querySelector('[data-allocation-summary]')?.classList).toContain('is-over');
      expect(el(fixture).querySelector('[data-allocation-error]')?.textContent).toContain('at most 100%');
      expect(saveButton(fixture).disabled).toBeTrue();
      saveButton(fixture).click();
      expect(createSpy).not.toHaveBeenCalled();
    });

    it('accepts exactly 100 and sends every row with its position and percentage', () => {
      const fixture = create(null);
      createSpy.and.returnValue(of({ ...existing, id: 41 }));
      fill(fixture);
      addRow(fixture);
      setPercent(fixture, 0, '70');
      addRow(fixture);

      submit(fixture);

      expect(saveButton(fixture).disabled).toBeFalse();
      expect(createSpy).toHaveBeenCalledWith(
        jasmine.objectContaining({
          projectAllocations: [
            { projectId: 1, positionId: 1, percent: 70 },
            { projectId: 2, positionId: 1, percent: 30 }
          ]
        })
      );
    });

    it('refuses a percentage that is not a whole number from 1 to 100', () => {
      const fixture = create(null);
      fill(fixture);
      addRow(fixture);

      setPercent(fixture, 0, '0');
      submit(fixture);
      expect(el(fixture).querySelector('[data-allocation-error]')?.textContent).toContain('whole percentage from 1 to 100');

      setPercent(fixture, 0, '12.5');
      submit(fixture);
      expect(createSpy).not.toHaveBeenCalled();
    });

    it('removes a row and frees its percentage', () => {
      const fixture = create(null);
      addRow(fixture);

      (el(fixture).querySelector('[data-row="0"] button') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(el(fixture).querySelectorAll('[data-row]').length).toBe(0);
      expect(summary(fixture)).toBe('0% allocated, 100% remaining');
    });

    it('hides a project already used by another row', () => {
      const fixture = create(null);
      addRow(fixture);
      addRow(fixture);

      const first = el(fixture).querySelector('[data-row="0"] select') as HTMLSelectElement;
      expect(Array.from(first.options).map((o) => o.text.trim())).toEqual(['Choose a project', 'Engineering']);
    });

    describe('when editing', () => {
      const seventy = {
        ...existing,
        projectAllocations: [{ projectId: 1, projectName: 'Engineering', positionId: 3, positionName: 'QA', percent: 70 }],
        totalAllocatedPercent: 70,
        remainingPercent: 30
      };

      it('shows the projects as a read-only list pointing to Project management', () => {
        const fixture = create(seventy);

        const view = el(fixture).querySelector('[data-view="project-allocations"]') as HTMLElement;
        expect(view.textContent).toContain('Engineering');
        expect(view.textContent).toContain('QA');
        expect(view.textContent).toContain('70%');
        expect(view.textContent).toContain('Project management');
        expect(summary(fixture)).toBe('70% allocated, 30% remaining');
        expect(el(fixture).querySelector('[data-action="add-project"]')).toBeNull();
        expect(el(fixture).querySelectorAll('[data-row]').length).toBe(0);
      });

      it('keeps the existing projects unchanged when saving', () => {
        const fixture = create(seventy);
        updateSpy.and.returnValue(of(seventy));

        submit(fixture);

        expect(updateSpy).toHaveBeenCalledWith(
          7,
          jasmine.objectContaining({ projectAllocations: [{ projectId: 1, positionId: 3, percent: 70 }] })
        );
      });

      it('still saves a person whose recorded projects already exceed 100%', () => {
        const over = {
          ...seventy,
          projectAllocations: [
            seventy.projectAllocations[0],
            { projectId: 2, projectName: 'Sales', positionId: 1, positionName: 'Member', percent: 60 }
          ]
        };
        const fixture = create(over);
        updateSpy.and.returnValue(of(seventy));

        submit(fixture);

        expect(updateSpy).toHaveBeenCalled();
      });
    });
  });

  describe('manager projects', () => {
    const managing = { ...manager, id: 21, managerProjects: [{ id: 1, name: 'Engineering' }, { id: 2, name: 'Sales' }] };

    it('lists the projects a manager manages as read-only, with no checkboxes', () => {
      const fixture = create(managing);

      const view = el(fixture).querySelector('[data-view="manager-projects"]') as HTMLElement;
      expect(view.textContent).toContain('Engineering');
      expect(view.textContent).toContain('Sales');
      expect(el(fixture).querySelectorAll('[data-project]').length).toBe(0);
      expect(el(fixture).querySelector('[data-field="manager-projects"]')).toBeNull();
    });

    it('shows nothing for an employee, for a manager without projects, or when adding', () => {
      expect(el(create({ ...existing, managerProjects: [{ id: 1, name: 'Engineering' }] })).querySelector('[data-view="manager-projects"]')).toBeNull();
      expect(el(create({ ...manager, managerProjects: [] })).querySelector('[data-view="manager-projects"]')).toBeNull();
      expect(el(create(null)).querySelector('[data-view="manager-projects"]')).toBeNull();
    });

    it('saves an edited manager through the normal update only', () => {
      const fixture = create(managing);
      updateSpy.and.returnValue(of(managing));

      type(fixture, 'emp-name', 'Anupama N');
      submit(fixture);

      expect(updateSpy).toHaveBeenCalledTimes(1);
      expect(toastSpy).toHaveBeenCalledWith('Anupama Nair updated');
    });
  });

  it('keeps only the part before the @ when a full address is pasted and saves it on the fixed domain', () => {
    const fixture = create(null);
    createSpy.and.returnValue(of({ ...existing, id: 11, fullName: 'Pia Paste' }));

    type(fixture, 'emp-name', 'Pia Paste');
    type(fixture, 'emp-username', 'pia.p');
    type(fixture, 'emp-email', 'Pia.Paste@other.com');
    type(fixture, 'emp-password', 'Temp#1234');
    submit(fixture);

    expect((el(fixture).querySelector('#emp-email') as HTMLInputElement).value).toBe('pia.paste');
    expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({ email: 'pia.paste@taskflow.com' }));
  });

  it('requires a password of at least 6 characters, like the API', () => {
    const fixture = create(null);
    type(fixture, 'emp-password', '12345');

    submit(fixture);

    expect(el(fixture).querySelector('#emp-password-error')?.textContent).toContain('Use at least 6 characters.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('lists the system roles and the custom roles for an administrator', () => {
    const fixture = create(null);

    const select = el(fixture).querySelector('#emp-role') as HTMLSelectElement;
    const names = Array.from(select.options).map((option) => option.text.trim());

    expect(select.tagName).toBe('SELECT');
    expect(names).toEqual(['Employee', 'Manager', 'Administrator', 'Team Lead']);
  });

  it('saves the id of the chosen custom role', () => {
    const fixture = create(null);
    createSpy.and.returnValue(of({ ...existing, id: 10, role: 'Team Lead', roleId: 9 }));

    type(fixture, 'emp-name', 'Lena Lead');
    type(fixture, 'emp-username', 'lena.l');
    type(fixture, 'emp-email', 'lena@taskflow.com');
    type(fixture, 'emp-password', 'Temp#1234');
    const select = el(fixture).querySelector('#emp-role') as HTMLSelectElement;
    select.value = Array.from(select.options).find((option) => option.text.trim() === 'Team Lead')!.value;
    select.dispatchEvent(new Event('change'));
    submit(fixture);

    expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({ roleId: 9 }));
  });

  it('shows the role read-only and keeps it when the user cannot manage roles', () => {
    currentUser.set({
      id: 2,
      fullName: 'People Manager',
      username: 'people',
      email: 'people@taskflow.com',
      role: 'People Managers',
      employeeCode: null,
      permissions: ['employees.manage']
    });
    const fixture = create(existing);
    updateSpy.and.returnValue(of({ ...existing, fullName: 'Rahul M' }));

    const field = el(fixture).querySelector('#emp-role') as HTMLInputElement;
    type(fixture, 'emp-name', 'Rahul M');
    submit(fixture);

    expect(field.tagName).toBe('INPUT');
    expect(field.readOnly).toBeTrue();
    expect(field.value).toBe('Employee');
    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ roleId: 3 }));
  });

  it('creates the employee, shows a success toast and emits saved', () => {
    const fixture = create(null);
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    createSpy.and.returnValue(of({ ...existing, id: 9, fullName: 'Divya Krishnan' }));

    type(fixture, 'emp-name', '  Divya Krishnan ');
    type(fixture, 'emp-username', 'divya.k');
    type(fixture, 'emp-email', 'divya@taskflow.com');
    type(fixture, 'emp-code', 'EMP-047');
    type(fixture, 'emp-password', 'Temp#1234');
    submit(fixture);

    expect(createSpy).toHaveBeenCalledWith(
      jasmine.objectContaining({
        fullName: 'Divya Krishnan',
        employeeCode: 'EMP-047',
        password: 'Temp#1234',
        isActive: true,
        roleId: 3
      })
    );
    expect(toastSpy).toHaveBeenCalledWith('Divya Krishnan added');
    expect(savedSpy).toHaveBeenCalled();
  });

  it('keeps the panel open and shows the server error on a 409 conflict', () => {
    const fixture = create(existing);
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    updateSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'Email is already in use.' } }))
    );

    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ email: 'rahul.menon@taskflow.com' }));
    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Email is already in use.');
    expect(savedSpy).not.toHaveBeenCalled();
    expect(toastSpy).not.toHaveBeenCalled();
  });

  describe('refreshed layout', () => {
    it('shows the identity header of the employee being edited', () => {
      const fixture = create({ ...existing, updatedAt: '2026-10-21T08:00:00Z' });
      fixture.detectChanges();

      const header = el(fixture).querySelector('[data-role="identity"]') as HTMLElement;
      expect(header.textContent).toContain('RM');
      expect(header.textContent).toContain('Rahul Menon');
      expect(header.textContent).toContain('rahul.menon@taskflow.com');
      expect(header.textContent).toContain('Employee');
      expect(header.textContent).toContain('Active');
      expect(header.textContent).toContain('EMP-022');
      expect(el(fixture).querySelector('[data-role="updated-at"]')?.textContent).toContain('Last updated');
    });

    it('shows a neutral header and no last-updated line when adding', () => {
      const fixture = create(null);

      const header = el(fixture).querySelector('[data-role="identity"]') as HTMLElement;
      expect(header.textContent).toContain('New employee');
      expect(header.textContent).not.toContain('EMP-');
      expect(el(fixture).querySelector('[data-role="updated-at"]')).toBeNull();
    });

    const titles = (fixture: ComponentFixture<EmployeeFormPanelComponent>) =>
      Array.from(el(fixture).querySelectorAll('.tf-section__title')).map((node) => node.textContent?.trim());

    it('groups the fields into four titled sections when adding', () => {
      expect(titles(create(null))).toEqual(['Personal details', 'Role and team', 'Project allocation', 'Sign-in and security']);
    });

    it('leaves out the sign-in section when editing', () => {
      expect(titles(create(existing))).toEqual(['Personal details', 'Role and team', 'Project allocation']);
    });

    it('toggles a department pill from the keyboard-reachable checkbox', () => {
      const fixture = create(null);
      const pill = el(fixture).querySelector('[data-department="5"]') as HTMLInputElement;

      pill.click();
      fixture.detectChanges();
      expect(pill.checked).toBeTrue();
      expect(pill.closest('.tf-option')).toBeTruthy();

      pill.click();
      fixture.detectChanges();
      expect(pill.checked).toBeFalse();
    });

    it('draws one bar segment per project when viewing and none when empty', () => {
      const fixture = create({
        ...existing,
        projectAllocations: [
          { projectId: 1, projectName: 'Engineering', positionId: 3, positionName: 'QA', percent: 60 },
          { projectId: 2, projectName: 'Sales', positionId: 1, positionName: 'Member', percent: 40 }
        ],
        totalAllocatedPercent: 100,
        remainingPercent: 0
      });

      const widths = Array.from(el(fixture).querySelectorAll('.tf-alloc-bar > i')).map((segment) => (segment as HTMLElement).style.width);
      expect(widths).toEqual(['60%', '40%']);
      expect(el(create({ ...existing, projectAllocations: [], totalAllocatedPercent: 0, remainingPercent: 100 })).querySelectorAll('.tf-alloc-bar > i').length).toBe(0);
    });

    it('turns the bar red when an added allocation goes over capacity', () => {
      const fixture = create(null);
      const percent = (row: number, value: string) => {
        const input = el(fixture).querySelector(`[data-row="${row}"] .allocation-row__percent`) as HTMLInputElement;
        input.value = value;
        input.dispatchEvent(new Event('input'));
        fixture.detectChanges();
      };
      const add = () => {
        (el(fixture).querySelector('[data-action="add-project"]') as HTMLButtonElement).click();
        fixture.detectChanges();
      };
      add();
      percent(0, '60');
      add();
      const bar = el(fixture).querySelector('.tf-alloc-bar') as HTMLElement;
      expect(bar.classList).not.toContain('is-over');

      percent(1, '60');

      expect(bar.classList).toContain('is-over');
      expect((el(fixture).querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBeTrue();
    });

    it('reveals and hides the typed password when adding', () => {
      const fixture = create(null);
      const input = el(fixture).querySelector('#emp-password') as HTMLInputElement;
      const toggle = el(fixture).querySelector('[data-action="toggle-password"]') as HTMLButtonElement;

      expect(input.type).toBe('password');
      expect(toggle.textContent?.trim()).toBe('Show');

      toggle.click();
      fixture.detectChanges();
      expect(input.type).toBe('text');
      expect(toggle.textContent?.trim()).toBe('Hide');
    });

    it('rates password strength without changing the validation rules', () => {
      const fixture = create(null);
      const lit = () => el(fixture).querySelectorAll('[data-role="password-strength"] i.is-on').length;

      type(fixture, 'emp-password', 'abc');
      fixture.detectChanges();
      expect(lit()).toBe(0);
      expect(fixture.componentInstance.form.controls.password.hasError('minlength')).toBeTrue();

      type(fixture, 'emp-password', 'Abcdef1!');
      fixture.detectChanges();
      expect(lit()).toBe(4);
      expect(fixture.componentInstance.form.controls.password.valid).toBeTrue();
    });

    it('shows the sign-out warning only when editing', () => {
      expect(el(create(existing)).querySelector('.tf-notice')?.textContent).toContain('signs this employee out');
      expect(el(create(null)).querySelector('.tf-notice')).toBeNull();
    });
  });

  it('disables Save while saving', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(new Subject<User>());

    submit(fixture);

    expect((el(fixture).querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBeTrue();
  });
});

describe('EmployeeDetailsPanelComponent manager details', () => {
  async function render(employee: User): Promise<HTMLElement> {
    await TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] }).compileComponents();
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', employee);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows an employee their managers and nothing about projects or a team', async () => {
    const element = await render({ ...existing, managers: [{ id: 20, fullName: 'Anupama Nair' }, { id: 21, fullName: 'Ravi Rao' }] });

    expect(element.querySelector('[data-field="managers"]')?.textContent?.trim()).toBe('Anupama Nair, Ravi Rao');
    expect(element.querySelector('[data-field="managed-projects"]')).toBeNull();
    expect(element.querySelector('[data-field="team"]')).toBeNull();
  });

  it('says there is no manager when none is assigned', async () => {
    const element = await render(existing);

    expect(element.querySelector('[data-field="managers"]')?.textContent?.trim()).toBe('None');
  });

  it('lists every project with its percentage and position, the total and the departments', async () => {
    const element = await render({
      ...existing,
      projectAllocations: [
        { projectId: 1, projectName: 'Apollo', positionId: 3, positionName: 'QA', percent: 60 },
        { projectId: 2, projectName: 'Zephyr', positionId: 4, positionName: 'Developer', percent: 40 }
      ],
      totalAllocatedPercent: 100,
      remainingPercent: 0,
      departments: [{ id: 5, name: 'Human Resources' }, { id: 6, name: 'Finance' }]
    });

    expect(element.querySelector('[data-field="projects"]')?.textContent?.trim()).toBe(
      'Apollo 60% (QA), Zephyr 40% (Developer) · 100% allocated'
    );
    expect(element.querySelector('[data-field="departments"]')?.textContent?.trim()).toBe('Human Resources, Finance');
  });

  it('shows a manager the projects they oversee and the people who report to them', async () => {
    const element = await render({
      ...existing,
      id: 20,
      role: 'Manager',
      roleId: 2,
      managerProjects: [
        { id: 1, name: 'Engineering' },
        { id: 3, name: 'Finance' }
      ],
      reports: [{ id: 7, fullName: 'Rahul Menon' }]
    });

    expect(element.querySelector('[data-field="managed-projects"]')?.textContent?.trim()).toBe('Engineering, Finance');
    expect(element.querySelector('[data-field="team"]')?.textContent?.trim()).toBe('Rahul Menon');
  });

  it('says so when a manager has no projects or team yet', async () => {
    const element = await render({ ...existing, id: 20, role: 'Manager', roleId: 2, managerProjects: [], reports: [] });

    expect(element.querySelector('[data-field="managed-projects"]')?.textContent?.trim()).toBe('None');
    expect(element.querySelector('[data-field="team"]')?.textContent?.trim()).toBe('No one yet');
  });
});

describe('EmployeeDetailsPanelComponent', () => {
  it('shows read-only details and emits edit', async () => {
    await TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] }).compileComponents();
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', existing);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const editSpy = jasmine.createSpy('edit');
    fixture.componentInstance.edit.subscribe(editSpy);

    expect(element.textContent).toContain('EMP-022');
    expect(element.textContent).toContain('rahul.m');
    expect(element.textContent).toContain('Engineering');
    expect(element.textContent).toContain('Never');
    expect(element.querySelector('input')).toBeNull();

    (element.querySelector('.tf-btn--primary') as HTMLButtonElement).click();
    expect(editSpy).toHaveBeenCalled();
  });

  it('shows the department name', async () => {
    await TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] }).compileComponents();
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', { ...existing, departments: [{ id: 5, name: 'Human Resources' }] });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-field="departments"]')?.textContent?.trim()).toBe('Human Resources');
  });

  it('says Unassigned when the employee has no department', async () => {
    await TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] }).compileComponents();
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', existing);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-field="departments"]')?.textContent?.trim()).toBe('Unassigned');
  });
});
