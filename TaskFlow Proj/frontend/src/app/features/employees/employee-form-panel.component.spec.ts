import { HttpErrorResponse } from '@angular/common/http';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { DepartmentWithCount, User } from '../../core/models';
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
  projectId: 1,
  projectName: 'Engineering'
};

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
  let assignSpy: jasmine.Spy;
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
      { id: 1, name: 'Engineering', description: null, createdAt: '2026-09-01', employeeCount: 1 }
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
    assignSpy = jasmine.createSpy('updateAssignments');
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of([manager]));

    currentUser.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.com', role: 'Administrator', employeeCode: null, permissions: systemPermissions('Administrator') });
    clearSessionSpy = jasmine.createSpy('clearSession');

    await TestBed.configureTestingModule({
      imports: [EmployeeFormPanelComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: { create: createSpy, update: updateSpy, getAll: getAllSpy, updateAssignments: assignSpy } },
        { provide: AuthService, useValue: fakeAuthService(currentUser.asReadonly(), { clearSession: clearSessionSpy }) },
        {
          provide: RoleService,
          useValue: {
            list: () =>
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

  it('keeps the current password when the new password is left blank', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(of({ ...existing, fullName: 'Rahul M' }));

    type(fixture, 'emp-name', 'Rahul M');
    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ password: null }));
    expect(toastSpy).toHaveBeenCalledWith('Rahul M updated');
  });

  it('sends a new password and warns that the employee must sign in again', () => {
    const fixture = create(existing);
    updateSpy.and.returnValue(of(existing));

    type(fixture, 'emp-password', 'NewPass#1');
    submit(fixture);

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ password: 'NewPass#1' }));
    expect(toastSpy).toHaveBeenCalledWith('Rahul Menon updated. They must sign in again.');
    expect(clearSessionSpy).not.toHaveBeenCalled();
  });

  it('rejects a new password shorter than 6 characters when editing', () => {
    const fixture = create(existing);

    type(fixture, 'emp-password', '123');
    submit(fixture);

    expect(el(fixture).querySelector('#emp-password-error')?.textContent).toContain('Use at least 6 characters.');
    expect(updateSpy).not.toHaveBeenCalled();
  });

  it('rejects a new password that is only spaces', () => {
    const fixture = create(existing);

    type(fixture, 'emp-password', '      ');
    submit(fixture);

    expect(el(fixture).querySelector('#emp-password-error')?.textContent).toContain('Password cannot be only spaces.');
    expect(updateSpy).not.toHaveBeenCalled();
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

  it('pre-fills the employee, offers an optional new password, and hides status when editing', () => {
    const fixture = create(existing);

    expect((el(fixture).querySelector('#emp-name') as HTMLInputElement).value).toBe('Rahul Menon');
    expect((el(fixture).querySelector('#emp-code') as HTMLInputElement).value).toBe('EMP-022');
    expect(el(fixture).querySelector('label[for="emp-password"]')?.textContent).toContain('New password');
    expect((el(fixture).querySelector('#emp-password') as HTMLInputElement).value).toBe('');
    expect(el(fixture).querySelector('#emp-password-hint')?.textContent).toContain('signs this employee out');
    expect(el(fixture).querySelector('#emp-status')).toBeNull();
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

  describe('department assignment', () => {
    function select(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: string, value: string): void {
      const field = el(fixture).querySelector(`#${id}`) as HTMLSelectElement;
      field.value = Array.from(field.options).find((option) => option.text.trim() === value)!.value;
      field.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    }

    it('offers the departments with an explicit unassigned choice', () => {
      const fixture = create(null);

      const selectField = el(fixture).querySelector('#emp-department') as HTMLSelectElement;
      const options = Array.from(selectField.options).map((option) => option.text.trim());

      expect(options).toEqual(['Unassigned', 'Human Resources', 'Finance']);
    });

    it('includes the chosen department in the create payload', () => {
      const fixture = create(null);
      createSpy.and.returnValue(of({ ...existing, id: 41, fullName: 'Nia New' }));

      type(fixture, 'emp-name', 'Nia New');
      type(fixture, 'emp-username', 'nia.n');
      type(fixture, 'emp-email', 'nia');
      type(fixture, 'emp-password', 'Temp#1234');
      select(fixture, 'emp-department', 'Human Resources');
      submit(fixture);

      expect(createSpy).toHaveBeenCalledWith(jasmine.objectContaining({ departmentId: 5 }));
    });

    it('includes the chosen department in the update payload', () => {
      const fixture = create(existing);
      updateSpy.and.returnValue(of({ ...existing, departmentId: 6 }));

      select(fixture, 'emp-department', 'Finance');
      submit(fixture);

      expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ departmentId: 6 }));
    });

    it('shows the department of the loaded user as selected', () => {
      const fixture = create({ ...existing, departmentId: 6, departmentName: 'Finance' });

      const selectField = el(fixture).querySelector('#emp-department') as HTMLSelectElement;
      expect(selectField.selectedOptions[0].text.trim()).toBe('Finance');
    });

    it('sends null when the department is cleared', () => {
      const fixture = create({ ...existing, departmentId: 5, departmentName: 'Human Resources' });
      updateSpy.and.returnValue(of(existing));

      select(fixture, 'emp-department', 'Unassigned');
      submit(fixture);

      expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ departmentId: null }));
    });
  });

  describe('manager assignments', () => {
    function select(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: string, value: string): void {
      const field = el(fixture).querySelector(`#${id}`) as HTMLSelectElement;
      field.value = Array.from(field.options).find((option) => option.text.trim() === value)!.value;
      field.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    }

    function projectBox(fixture: ComponentFixture<EmployeeFormPanelComponent>, id: number): HTMLInputElement | null {
      return el(fixture).querySelector(`[data-project="${id}"]`);
    }

    it('offers the active managers but not the employee being edited', () => {
      getAllSpy.and.returnValue(of([manager, { ...manager, id: 7, fullName: 'Rahul Menon' }]));
      const fixture = create(existing);

      const options = Array.from((el(fixture).querySelector('#emp-manager') as HTMLSelectElement).options).map((o) => o.text.trim());

      expect(getAllSpy).toHaveBeenCalledWith({ role: 'Manager', isActive: true });
      expect(options).toEqual(['No manager', 'Anupama Nair']);
    });

    it('keeps an inactive current manager in the list, marked inactive, so the select is not blank', () => {
      const fixture = create({ ...existing, managerId: 55, managerName: 'Old Boss' });
      fixture.detectChanges();

      const select = el(fixture).querySelector('#emp-manager') as HTMLSelectElement;
      const options = Array.from(select.options).map((option) => option.text.trim());

      expect(options).toEqual(['No manager', 'Anupama Nair', 'Old Boss (inactive)']);
      expect(select.selectedOptions[0].text.trim()).toBe('Old Boss (inactive)');
    });

    it('is not offered to someone who can manage employees but is not an Administrator', () => {
      currentUser.set({ id: 2, fullName: 'People', username: 'people', email: 'people@taskflow.com', role: 'People', employeeCode: null, permissions: ['employees.manage'] });
      const fixture = create(existing);

      expect(el(fixture).querySelector('#emp-manager')).toBeNull();
      expect(getAllSpy).not.toHaveBeenCalled();
    });

    it('shows the project choices only while the Manager role is chosen', () => {
      const fixture = create(null);
      expect(projectBox(fixture, 1)).toBeNull();

      select(fixture, 'emp-role', 'Manager');

      expect(projectBox(fixture, 1)).not.toBeNull();
      select(fixture, 'emp-role', 'Employee');
      expect(projectBox(fixture, 1)).toBeNull();
    });

    it('assigns the chosen manager to a new employee once they are created', () => {
      const fixture = create(null);
      createSpy.and.returnValue(of({ ...existing, id: 31, fullName: 'Nia New' }));
      assignSpy.and.returnValue(of({ ...existing, id: 31, fullName: 'Nia New', managerId: 20, managerName: 'Anupama Nair' }));
      const savedSpy = jasmine.createSpy('saved');
      fixture.componentInstance.saved.subscribe(savedSpy);

      type(fixture, 'emp-name', 'Nia New');
      type(fixture, 'emp-username', 'nia.n');
      type(fixture, 'emp-email', 'nia');
      type(fixture, 'emp-password', 'Temp#1234');
      select(fixture, 'emp-manager', 'Anupama Nair');
      submit(fixture);

      expect(assignSpy).toHaveBeenCalledOnceWith(31, { managerId: 20, managerProjectIds: [] });
      expect(savedSpy).toHaveBeenCalledWith(jasmine.objectContaining({ managerName: 'Anupama Nair' }));
    });

    it('assigns the ticked projects to a manager', () => {
      const fixture = create({ ...manager, id: 21, managerProjects: [] });
      updateSpy.and.returnValue(of({ ...manager, id: 21 }));
      assignSpy.and.returnValue(of({ ...manager, id: 21, managerProjects: [{ id: 1, name: 'Engineering' }] }));

      const box = projectBox(fixture, 1)!;
      box.checked = true;
      box.dispatchEvent(new Event('change'));
      submit(fixture);

      expect(assignSpy).toHaveBeenCalledOnceWith(21, { managerId: null, managerProjectIds: [1] });
    });

    it('does not call the assignments endpoint when nothing about them changed', () => {
      const fixture = create({ ...existing, managerId: 20, managerName: 'Anupama Nair' });
      updateSpy.and.returnValue(of(existing));

      type(fixture, 'emp-name', 'Rahul M');
      submit(fixture);

      expect(updateSpy).toHaveBeenCalled();
      expect(assignSpy).not.toHaveBeenCalled();
    });

    it('sends no projects for someone who is not a manager', () => {
      const fixture = create({ ...existing, managerId: 20 });
      updateSpy.and.returnValue(of(existing));
      assignSpy.and.returnValue(of(existing));

      select(fixture, 'emp-manager', 'No manager');
      submit(fixture);

      expect(assignSpy).toHaveBeenCalledOnceWith(7, { managerId: null, managerProjectIds: [] });
    });

    it('still reports the employee as saved when only the assignments are rejected', () => {
      const fixture = create(null);
      createSpy.and.returnValue(of({ ...existing, id: 32, fullName: 'Omar Odd' }));
      assignSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'The manager must be an active Manager.' } })));
      const savedSpy = jasmine.createSpy('saved');
      fixture.componentInstance.saved.subscribe(savedSpy);

      type(fixture, 'emp-name', 'Omar Odd');
      type(fixture, 'emp-username', 'omar.o');
      type(fixture, 'emp-email', 'omar');
      type(fixture, 'emp-password', 'Temp#1234');
      select(fixture, 'emp-manager', 'Anupama Nair');
      submit(fixture);

      expect(errorToastSpy).toHaveBeenCalledWith('Omar Odd was saved, but The manager must be an active Manager.');
      expect(savedSpy).toHaveBeenCalled();
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

  it('shows an employee their manager and nothing about projects or a team', async () => {
    const element = await render({ ...existing, managerId: 20, managerName: 'Anupama Nair' });

    expect(element.querySelector('[data-field="manager"]')?.textContent?.trim()).toBe('Anupama Nair');
    expect(element.querySelector('[data-field="managed-projects"]')).toBeNull();
    expect(element.querySelector('[data-field="team"]')).toBeNull();
  });

  it('says there is no manager when none is assigned', async () => {
    const element = await render(existing);

    expect(element.querySelector('[data-field="manager"]')?.textContent?.trim()).toBe('None');
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
    fixture.componentRef.setInput('employee', { ...existing, departmentId: 5, departmentName: 'Human Resources' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-field="department"]')?.textContent?.trim()).toBe('Human Resources');
  });

  it('says Unassigned when the employee has no department', async () => {
    await TestBed.configureTestingModule({ imports: [EmployeeDetailsPanelComponent] }).compileComponents();
    const fixture = TestBed.createComponent(EmployeeDetailsPanelComponent);
    fixture.componentRef.setInput('employee', existing);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-field="department"]')?.textContent?.trim()).toBe('Unassigned');
  });
});
