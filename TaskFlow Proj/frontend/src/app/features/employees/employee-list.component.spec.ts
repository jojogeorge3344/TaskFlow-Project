import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { Permissions } from '../../core/auth/permissions';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { ComponentFixture, TestBed, fakeAsync, flush, flushMicrotasks, tick } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DepartmentWithCount, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { RoleService } from '../../core/services/role.service';
import { UserService } from '../../core/services/user.service';
import { ToastService } from '../../shared/ui/toast.service';
import { EmployeeListComponent, SEARCH_DEBOUNCE_MS } from './employee-list.component';

describe('EmployeeListComponent', () => {
  const departments: DepartmentWithCount[] = [
    { id: 1, name: 'Engineering', description: null, createdAt: '2026-09-01', employeeCount: 2 },
    { id: 3, name: 'Finance', description: null, createdAt: '2026-09-01', employeeCount: 1 }
  ];

  function user(overrides: Partial<User>): User {
    return {
      id: 1,
      employeeCode: 'EMP-001',
      fullName: 'Rahul Menon',
      username: 'rahul.m',
      email: 'rahul.menon@taskflow.com',
      role: 'Employee',
      roleId: 3,
      isActive: true,
      createdAt: '2026-09-05T00:00:00Z',
      lastLoginAt: null,
      departmentId: 1,
      departmentName: 'Engineering',
      ...overrides
    };
  }

  const employees: User[] = [
    user({ id: 1 }),
    user({ id: 2, fullName: 'Anupama Nair', username: 'anupama.n', role: 'Manager', departmentId: null, departmentName: null })
  ];

  let getAllSpy: jasmine.Spy;
  let updateStatusSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;
  const currentUser = signal<{ id: number; permissions: string[] } | null>(null);
  let queryParams: Record<string, string>;

  function configure(): void {
    TestBed.configureTestingModule({
      imports: [EmployeeListComponent],
      providers: [
        provideRouter([]),
        { provide: UserService, useValue: { getAll: getAllSpy, updateStatus: updateStatusSpy, delete: deleteSpy } },
        { provide: DepartmentService, useValue: { getAll: () => of(departments) } },
        { provide: AuthService, useValue: fakeAuthService(currentUser, { clearSession: () => undefined }) },
        { provide: RoleService, useValue: { list: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } }
      ]
    });
  }

  function create(): ComponentFixture<EmployeeListComponent> {
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(EmployeeListComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<EmployeeListComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function closeOpenDialogs(): void {
    document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close());
  }

  beforeEach(() => {
    queryParams = {};
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(employees));
    updateStatusSpy = jasmine.createSpy('updateStatus').and.returnValue(of(employees[0]));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));
    currentUser.set(null);
  });

  describe('delete', () => {
    it('is not offered to a Manager, even one who can manage employees', () => {
      currentUser.set({ id: 50, permissions: [Permissions.EmployeesView, Permissions.EmployeesManage] });
      configure();
      const fixture = create();

      expect(el(fixture).querySelector('[data-action="delete"]')).toBeNull();
    });

    it('is offered to an Administrator on every row except their own', () => {
      currentUser.set({ id: 2, permissions: systemPermissions('Administrator') });
      configure();
      const fixture = create();

      const rows = Array.from(el(fixture).querySelectorAll('tr.employee-row'));
      expect(rows[0].querySelector('[data-action="delete"]')).not.toBeNull();
      expect(rows[1].querySelector('[data-action="delete"]')).toBeNull();
    });

    it('does nothing when the confirmation is cancelled', fakeAsync(() => {
      currentUser.set({ id: 99, permissions: systemPermissions('Administrator') });
      configure();
      const fixture = create();

      (el(fixture).querySelector('tr.employee-row [data-action="delete"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      const dialog = el(fixture).querySelector('dialog') as HTMLDialogElement;
      expect(dialog.open).toBeTrue();
      expect(dialog.textContent).toContain('Rahul Menon');
      (dialog.querySelector('[data-action="cancel"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(deleteSpy).not.toHaveBeenCalled();
    }));

    it('deletes after confirmation, shows a toast and reloads the list', fakeAsync(() => {
      currentUser.set({ id: 99, permissions: systemPermissions('Administrator') });
      configure();
      const fixture = create();
      const toastSpy = spyOn(TestBed.inject(ToastService), 'success');
      getAllSpy.calls.reset();

      (el(fixture).querySelector('tr.employee-row [data-action="delete"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(deleteSpy).toHaveBeenCalledWith(1);
      expect(toastSpy).toHaveBeenCalledWith('Rahul Menon deleted');
      expect(getAllSpy).toHaveBeenCalled();
    }));

    it('shows the server message when the delete is rejected', fakeAsync(() => {
      currentUser.set({ id: 99, permissions: systemPermissions('Administrator') });
      deleteSpy.and.returnValue(
        throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Rahul Menon still has 2 unfinished tasks.' } }))
      );
      configure();
      const fixture = create();
      const errorSpy = spyOn(TestBed.inject(ToastService), 'error');

      (el(fixture).querySelector('tr.employee-row [data-action="delete"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(errorSpy).toHaveBeenCalledWith('Rahul Menon still has 2 unfinished tasks.');
    }));
  });

  afterEach(() => closeOpenDialogs());

  it('renders the header count, badges and the Unassigned fallback', () => {
    configure();
    const fixture = create();

    expect(el(fixture).querySelector('h1')?.textContent).toContain('Employee Management');
    expect(el(fixture).querySelector('.tf-page-header p')?.textContent).toContain('2 employees');
    expect(el(fixture).querySelectorAll('tr.employee-row').length).toBe(2);
    expect(el(fixture).querySelector('.tf-badge--manager')?.textContent).toContain('Manager');
    expect(el(fixture).querySelector('.tf-badge--active')?.textContent).toContain('Active');
    expect(el(fixture).querySelector('.unassigned')?.textContent).toContain('Unassigned');
  });

  it('applies the search only after the user stops typing for 300ms', fakeAsync(() => {
    configure();
    const fixture = create();
    getAllSpy.calls.reset();

    const input = el(fixture).querySelector('#emp-search') as HTMLInputElement;
    input.value = 'anu';
    input.dispatchEvent(new Event('input'));

    tick(SEARCH_DEBOUNCE_MS - 1);
    expect(getAllSpy).not.toHaveBeenCalled();

    tick(1);
    expect(getAllSpy).toHaveBeenCalledOnceWith({ search: 'anu' });
  }));

  it('applies a select filter immediately', () => {
    configure();
    const fixture = create();
    getAllSpy.calls.reset();

    const select = el(fixture).querySelector('#emp-filter-status') as HTMLSelectElement;
    select.value = 'inactive';
    select.dispatchEvent(new Event('change'));

    expect(getAllSpy).toHaveBeenCalledOnceWith({ isActive: false });
  });

  it('pre-filters by the departmentId query parameter', () => {
    queryParams = { departmentId: '3' };
    configure();
    const fixture = create();

    expect(getAllSpy).toHaveBeenCalledWith({ departmentId: 3 });
    const select = el(fixture).querySelector('#emp-filter-department') as HTMLSelectElement;
    expect(select.value).toBe('3');
  });

  it('writes the department filter back to the URL', () => {
    configure();
    const fixture = create();
    const navigateSpy = TestBed.inject(Router).navigate as jasmine.Spy;

    fixture.componentInstance.setDepartment('1');

    expect(navigateSpy).toHaveBeenCalledWith(
      [],
      jasmine.objectContaining({ queryParams: { departmentId: 1 }, replaceUrl: true })
    );
  });

  it('shows an empty state with Clear filters when filters match nothing', () => {
    configure();
    const fixture = create();
    getAllSpy.and.returnValue(of([]));

    fixture.componentInstance.setRole('Administrator');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No employees match these filters');
    const clear = Array.from(el(fixture).querySelectorAll('.tf-empty button')).find((b) =>
      b.textContent?.includes('Clear filters')
    ) as HTMLButtonElement;
    expect(clear).toBeTruthy();

    getAllSpy.and.returnValue(of(employees));
    clear.click();
    fixture.detectChanges();

    expect(getAllSpy).toHaveBeenCalledWith({});
    expect(el(fixture).querySelectorAll('tr.employee-row').length).toBe(2);
  });

  it('shows an error with a Retry button that reloads the list', () => {
    getAllSpy.and.returnValue(throwError(() => new Error('offline')));
    configure();
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load employees');

    getAllSpy.and.returnValue(of(employees));
    (el(fixture).querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelector('[role="alert"]')).toBeNull();
    expect(el(fixture).querySelectorAll('tr.employee-row').length).toBe(2);
  });

  it('opens the add form and the details view in the side panel', fakeAsync(() => {
    configure();
    const fixture = create();

    (el(fixture).querySelector('.tf-page-header .tf-btn--primary') as HTMLButtonElement).click();
    fixture.detectChanges();
    flush();

    expect(el(fixture).querySelector('.tf-drawer h2')?.textContent).toContain('Add employee');
    expect(el(fixture).querySelector('#emp-password')).toBeTruthy();
    expect(document.activeElement?.id).toBe('emp-name');

    fixture.componentInstance.closePanel();
    fixture.detectChanges();
    (el(fixture).querySelector('.person__name') as HTMLButtonElement).click();
    fixture.detectChanges();
    flush();

    expect(el(fixture).querySelector('.tf-drawer h2')?.textContent).toContain('Employee details');
    expect(el(fixture).querySelector('.tf-drawer')?.textContent).toContain('EMP-001');
  }));

  it('does not deactivate when the confirmation is cancelled', fakeAsync(() => {
    configure();
    const fixture = create();

    (el(fixture).querySelector('tr.employee-row [data-action="toggle-status"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.textContent).toContain('Rahul Menon');

    (dialog.querySelector('[data-action="cancel"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(updateStatusSpy).not.toHaveBeenCalled();
  }));

  it('deactivates after confirmation and shows a toast', fakeAsync(() => {
    configure();
    const fixture = create();
    const toastSpy = spyOn(TestBed.inject(ToastService), 'success');

    (el(fixture).querySelector('tr.employee-row [data-action="toggle-status"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(updateStatusSpy).toHaveBeenCalledWith(1, { isActive: false });
    expect(toastSpy).toHaveBeenCalledWith('Rahul Menon deactivated');
  }));

  it('activates an inactive employee without confirmation', fakeAsync(() => {
    getAllSpy.and.returnValue(of([user({ id: 5, fullName: 'Meera Thomas', isActive: false })]));
    configure();
    const fixture = create();

    (el(fixture).querySelector('tr.employee-row [data-action="toggle-status"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect((el(fixture).querySelector('dialog') as HTMLDialogElement).open).toBeFalse();
    expect(updateStatusSpy).toHaveBeenCalledWith(5, { isActive: true });
  }));
});
