import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DepartmentMembers, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { UserService } from '../../core/services/user.service';
import { ToastService } from '../../shared/ui/toast.service';
import { DepartmentMembersDialogComponent } from './department-members-dialog.component';

describe('DepartmentMembersDialogComponent', () => {
  const department = { id: 1, name: 'Engineering', description: null, createdAt: '2026-09-01' };

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
      projectId: null,
      projectName: null,
      ...overrides
    };
  }

  const activeManagers: User[] = [user({ id: 5, fullName: 'Mary Manager', username: 'mary', role: 'Manager', roleId: 2 })];
  const activeEmployees: User[] = [user({ id: 9, fullName: 'Evan Employee', username: 'evan' })];

  /** Mary is an active manager; Sam is an inactive employee who still belongs to the department. */
  const members: DepartmentMembers = {
    departmentId: 1,
    managers: [{ id: 5, fullName: 'Mary Manager', username: 'mary', email: 'mary@taskflow.com', isActive: true }],
    employees: [{ id: 7, fullName: 'Sam Stopped', username: 'sam', email: 'sam@taskflow.com', isActive: false }]
  };

  let getMembersSpy: jasmine.Spy;
  let replaceMembersSpy: jasmine.Spy;
  let getAllUsersSpy: jasmine.Spy;
  let toastSpy: jasmine.Spy;
  let fixture: ComponentFixture<DepartmentMembersDialogComponent>;

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function dialog(): HTMLDialogElement {
    return el().querySelector('dialog')!;
  }

  function checkbox(field: 'managers' | 'employees', userId: number): HTMLInputElement {
    return el().querySelector(`[data-field="${field}"] [data-user="${userId}"]`) as HTMLInputElement;
  }

  function toggle(field: 'managers' | 'employees', userId: number, checked: boolean): void {
    const input = checkbox(field, userId);
    input.checked = checked;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function saveButton(): HTMLButtonElement {
    return el().querySelector('[data-action="save"]') as HTMLButtonElement;
  }

  beforeEach(async () => {
    getMembersSpy = jasmine.createSpy('getMembers').and.returnValue(of(members));
    replaceMembersSpy = jasmine.createSpy('replaceMembers').and.returnValue(of(members));
    getAllUsersSpy = jasmine.createSpy('getAll').and.callFake((params?: { role?: string }) =>
      params?.role === 'Manager' ? of(activeManagers) : of(activeEmployees)
    );

    await TestBed.configureTestingModule({
      imports: [DepartmentMembersDialogComponent],
      providers: [
        { provide: DepartmentService, useValue: { getMembers: getMembersSpy, replaceMembers: replaceMembersSpy } },
        { provide: UserService, useValue: { getAll: getAllUsersSpy } }
      ]
    }).compileComponents();

    toastSpy = spyOn(TestBed.inject(ToastService), 'success');
    fixture = TestBed.createComponent(DepartmentMembersDialogComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    if (dialog().open) {
      dialog().close();
    }
  });

  it('loads the members and both pickers in parallel and checks the current members', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    expect(dialog().open).toBeTrue();
    expect(getMembersSpy).toHaveBeenCalledWith(1);
    expect(getAllUsersSpy).toHaveBeenCalledWith({ role: 'Manager', isActive: true });
    expect(getAllUsersSpy).toHaveBeenCalledWith({ role: 'Employee', isActive: true });
    expect(checkbox('managers', 5).checked).toBeTrue();
    expect(checkbox('employees', 7).checked).toBeTrue();
  });

  it('keeps an inactive current member listed and checked with an Inactive hint', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    const inactiveRow = checkbox('employees', 7).closest('label')!;
    expect(inactiveRow.textContent).toContain('Sam Stopped');
    expect(inactiveRow.textContent).toContain('Inactive');
    expect(checkbox('employees', 7).checked).toBeTrue();
  });

  it('sends the full checked id sets, including the inactive member, on save', () => {
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    toggle('employees', 9, true);

    saveButton().click();

    expect(replaceMembersSpy).toHaveBeenCalledWith(1, { managerIds: [5], employeeIds: [7, 9] });
    expect(toastSpy).toHaveBeenCalledWith('Engineering members updated');
    expect(dialog().open).toBeFalse();
    expect(savedSpy).toHaveBeenCalled();
  });

  it('drops a member that is unchecked and keeps the rest', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    toggle('employees', 7, false);
    saveButton().click();

    expect(replaceMembersSpy).toHaveBeenCalledWith(1, { managerIds: [5], employeeIds: [] });
  });

  it('keeps the dialog open and shows the backend error when saving fails', () => {
    replaceMembersSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'User 9 is not an active user.' } }))
    );
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    saveButton().click();
    fixture.detectChanges();

    expect(dialog().open).toBeTrue();
    expect(el().querySelector('.tf-alert')?.textContent).toContain('User 9 is not an active user.');
    expect(toastSpy).not.toHaveBeenCalled();
    expect(savedSpy).not.toHaveBeenCalled();
  });

  it('shows the load error when the members could not be loaded', () => {
    getMembersSpy.and.returnValue(throwError(() => new Error('offline')));
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    expect(el().querySelector('.tf-alert')?.textContent).toContain('Could not load the department members.');
    expect(saveButton().disabled).toBeTrue();
  });
});
