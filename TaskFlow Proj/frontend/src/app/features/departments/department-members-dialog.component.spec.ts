import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DepartmentMembers, User } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { UserService } from '../../core/services/user.service';
import { ToastService } from '../../shared/ui/toast.service';
import { DepartmentMembersDialogComponent } from './department-members-dialog.component';
import { NO_ALLOCATIONS, withProject } from '../../testing/users';

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
      ...NO_ALLOCATIONS,
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

  function checkbox(userId: number): HTMLInputElement {
    return el().querySelector(`[data-user="${userId}"]`) as HTMLInputElement;
  }

  function toggle(userId: number, checked: boolean): void {
    const input = checkbox(userId);
    input.checked = checked;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function shownNames(): string[] {
    return Array.from(el().querySelectorAll('.department-members__name')).map((node) => node.textContent!.trim());
  }

  function search(text: string): void {
    const input = el().querySelector('#dm-search') as HTMLInputElement;
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function sortBy(value: string): void {
    const select = el().querySelector('#dm-sort') as HTMLSelectElement;
    select.value = value;
    select.dispatchEvent(new Event('change'));
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
    expect(checkbox(5).checked).toBeTrue();
    expect(checkbox(7).checked).toBeTrue();
    expect(checkbox(9).checked).toBeFalse();
    expect(el().querySelector('[data-role="selected-count"]')?.textContent).toContain('2 selected');
  });

  it('lists managers and employees together, each tagged with their role', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    expect(checkbox(5).closest('label')!.textContent).toContain('Manager');
    expect(checkbox(9).closest('label')!.textContent).toContain('Employee');
  });

  it('keeps an inactive current member listed and checked with an Inactive hint', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    const inactiveRow = checkbox(7).closest('label')!;
    expect(inactiveRow.textContent).toContain('Sam Stopped');
    expect(inactiveRow.textContent).toContain('Inactive');
    expect(checkbox(7).checked).toBeTrue();
  });

  it('selects several people and sends managers and employees separately on save', () => {
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    toggle(9, true);

    saveButton().click();

    expect(replaceMembersSpy).toHaveBeenCalledWith(1, { managerIds: [5], employeeIds: [7, 9] });
    expect(toastSpy).toHaveBeenCalledWith('Engineering members updated');
    expect(dialog().open).toBeFalse();
    expect(savedSpy).toHaveBeenCalled();
  });

  it('drops a member that is unchecked and keeps the rest', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    toggle(7, false);
    saveButton().click();

    expect(replaceMembersSpy).toHaveBeenCalledWith(1, { managerIds: [5], employeeIds: [] });
  });

  it('filters the list by name or username and says so when nobody matches', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    search('EVAN');
    expect(shownNames()).toEqual(['Evan Employee']);

    search('mary');
    expect(shownNames()).toEqual(['Mary Manager']);

    search('nobody');
    expect(shownNames()).toEqual([]);
    expect(el().querySelector('[data-empty]')?.textContent).toContain('No one matches "nobody"');
  });

  it('keeps a selection made before searching, even for people the search hides', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    toggle(9, true);
    search('mary');
    saveButton().click();

    expect(replaceMembersSpy).toHaveBeenCalledWith(1, { managerIds: [5], employeeIds: [7, 9] });
  });

  it('sorts by name in both directions, selected first, and by role', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    expect(shownNames()).toEqual(['Evan Employee', 'Mary Manager', 'Sam Stopped']);

    sortBy('name-desc');
    expect(shownNames()).toEqual(['Sam Stopped', 'Mary Manager', 'Evan Employee']);

    sortBy('selected');
    expect(shownNames()).toEqual(['Mary Manager', 'Sam Stopped', 'Evan Employee']);

    sortBy('role');
    expect(shownNames()).toEqual(['Evan Employee', 'Sam Stopped', 'Mary Manager']);
  });

  it('selects and clears everyone shown, leaving hidden people alone', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();
    const toggleAll = () => el().querySelector('[data-action="toggle-all"]') as HTMLButtonElement;

    search('evan');
    toggleAll().click();
    fixture.detectChanges();
    expect(checkbox(9).checked).toBeTrue();
    expect(el().querySelector('[data-role="selected-count"]')?.textContent).toContain('3 selected');
    expect(toggleAll().textContent).toContain('Clear shown');

    toggleAll().click();
    fixture.detectChanges();
    expect(checkbox(9).checked).toBeFalse();
    expect(el().querySelector('[data-role="selected-count"]')?.textContent).toContain('2 selected');
  });

  it('clears the whole selection', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();

    (el().querySelector('[data-action="clear"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    saveButton().click();

    expect(replaceMembersSpy).toHaveBeenCalledWith(1, { managerIds: [], employeeIds: [] });
  });

  it('starts every opening with an empty search and the default sort', () => {
    fixture.componentInstance.open(department);
    fixture.detectChanges();
    search('mary');
    sortBy('role');
    dialog().close();

    fixture.componentInstance.open(department);
    fixture.detectChanges();

    expect((el().querySelector('#dm-search') as HTMLInputElement).value).toBe('');
    expect(shownNames()).toEqual(['Evan Employee', 'Mary Manager', 'Sam Stopped']);
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
