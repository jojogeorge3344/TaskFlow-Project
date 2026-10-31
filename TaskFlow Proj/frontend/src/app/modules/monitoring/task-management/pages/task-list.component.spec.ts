import { signal } from '@angular/core';
import { fakeAuthService, systemPermissions } from '../../../../shared/testing/permissions';
import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { ToastService } from '../../../../shared/services/toast.service';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { AuthUser, UserRole } from '../../../../shared/auth/auth.models';
import { PagedResult, TaskListItem } from '../models/task.model';
import { PAGE_SIZE, SEARCH_DEBOUNCE_MS, TaskListComponent, endOfLocalDay, startOfLocalDay } from './task-list.component';
import { ProjectService } from '../../../management/project-management/services/project.service';
import { TaskService } from '../services/task.service';

describe('TaskListComponent', () => {
  function item(overrides: Partial<TaskListItem>): TaskListItem {
    return {
      id: 1,
      taskCode: 'TSK-000001',
      title: 'Design mockups',
      assignedToUserId: 3,
      assignedToName: 'Joe Thomas',
      createdByUserId: 1,
      createdByName: 'Maya Manager',
      priority: 'High',
      startDate: '2026-10-10T08:00:00Z',
      deadline: '2026-10-20T10:00:00Z',
      estimatedHours: 6,
      status: 'InProgress',
      stage: 'Upcoming',
      isOverdue: false,
      projectId: 4,
      projectName: 'Apollo',
      ...overrides
    };
  }

  function page(items: TaskListItem[], total = items.length, pageNumber = 1): PagedResult<TaskListItem> {
    return { items, total, page: pageNumber, pageSize: PAGE_SIZE };
  }

  let listSpy: jasmine.Spy;
  let assigneesSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;
  let projectsSpy: jasmine.Spy;

  function create(role: UserRole, queryParams: Record<string, string> = {}): ComponentFixture<TaskListComponent> {
    const user: AuthUser = { id: 1, fullName: 'Test', username: 'test', email: 't@x', role, employeeCode: null, permissions: systemPermissions(role) };
    TestBed.configureTestingModule({
      imports: [TaskListComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
        { provide: TaskService, useValue: { list: listSpy, getAssignees: assigneesSpy, delete: deleteSpy } },
        { provide: ProjectService, useValue: { getAll: projectsSpy } },
        { provide: AuthService, useValue: fakeAuthService(signal(user).asReadonly()) }
      ]
    });
    const fixture = TestBed.createComponent(TaskListComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<TaskListComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function lastFilter(): Record<string, unknown> {
    return listSpy.calls.mostRecent().args[0] as Record<string, unknown>;
  }

  beforeEach(() => {
    listSpy = jasmine.createSpy('list').and.returnValue(
      of(
        page([
          item({}),
          item({ id: 2, taskCode: 'TSK-000002', title: 'Late report', stage: 'Overdue', isOverdue: true, priority: 'Critical' })
        ])
      )
    );
    assigneesSpy = jasmine.createSpy('getAssignees').and.returnValue(of([{ userId: 3, fullName: 'Joe Thomas' }]));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));
    projectsSpy = jasmine.createSpy('getAll').and.returnValue(
      of([
        { id: 4, name: 'Apollo', description: null, createdAt: '2026-10-01T00:00:00Z', employeeCount: 2, managers: [] },
        { id: 5, name: 'Zeus', description: null, createdAt: '2026-10-01T00:00:00Z', employeeCount: 1, managers: [] }
      ])
    );
  });

  describe('project', () => {
    it('shows the project of each task, or a dash for a legacy task', () => {
      listSpy.and.returnValue(of(page([item({}), item({ id: 2, projectId: null, projectName: null })])));
      const fixture = create('Manager');

      const cells = Array.from(el(fixture).querySelectorAll('tr.task-row [data-project]')).map((cell) => cell.textContent?.trim());
      expect(cells).toEqual(['Apollo', '—']);
    });

    it('filters by project from page 1 and resets with the other filters', () => {
      const fixture = create('Manager');
      const select = el(fixture).querySelector('#task-filter-project') as HTMLSelectElement;
      expect(select.options.length).toBe(3);

      select.value = '4';
      select.dispatchEvent(new Event('change'));

      expect(lastFilter()['projectId']).toBe(4);
      expect(lastFilter()['page']).toBe(1);

      fixture.detectChanges();
      (Array.from(el(fixture).querySelectorAll('button')).find((b) => b.textContent?.includes('Reset')) as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(lastFilter()['projectId']).toBeUndefined();
      expect((el(fixture).querySelector('#task-filter-project') as HTMLSelectElement).value).toBe('');
    });

    it('combines the project with another filter', () => {
      const fixture = create('Manager');
      const select = el(fixture).querySelector('#task-filter-project') as HTMLSelectElement;
      select.value = '4';
      select.dispatchEvent(new Event('change'));
      const status = el(fixture).querySelector('#task-filter-status') as HTMLSelectElement;
      status.value = 'ToDo';
      status.dispatchEvent(new Event('change'));

      expect(lastFilter()['projectId']).toBe(4);
      expect(lastFilter()['status']).toBe('ToDo');
    });

    it('hides the filter when projects cannot be loaded', () => {
      projectsSpy.and.returnValue(throwError(() => new Error('403')));
      const fixture = create('Manager');

      expect(el(fixture).querySelector('#task-filter-project')).toBeNull();
      expect(el(fixture).querySelectorAll('tr.task-row').length).toBe(2);
    });
  });

  describe('copy', () => {
    it('is offered to a Manager on every row and links to the pre-filled create form', () => {
      const fixture = create('Manager');

      const links = Array.from(el(fixture).querySelectorAll('tr.task-row [data-action="copy"]'));
      expect(links.length).toBe(2);
      expect(links[0].getAttribute('href')).toBe('/tasks/new?copyFrom=1');
    });

    it('is not offered to an Employee', () => {
      const fixture = create('Employee');

      expect(el(fixture).querySelector('[data-action="copy"]')).toBeNull();
    });
  });

  describe('delete', () => {
    afterEach(() => document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close()));

    it('is not offered to a Manager', () => {
      const fixture = create('Manager');

      expect(el(fixture).querySelector('[data-action="delete"]')).toBeNull();
    });

    it('is not offered to an Employee', () => {
      const fixture = create('Employee');

      expect(el(fixture).querySelector('[data-action="delete"]')).toBeNull();
    });

    it('is offered to an Administrator on every row', () => {
      const fixture = create('Administrator');

      expect(el(fixture).querySelectorAll('tr.task-row [data-action="delete"]').length).toBe(2);
    });

    it('does nothing when the confirmation is cancelled', fakeAsync(() => {
      const fixture = create('Administrator');

      (el(fixture).querySelector('tr.task-row [data-action="delete"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      const dialog = el(fixture).querySelector('dialog') as HTMLDialogElement;
      expect(dialog.open).toBeTrue();
      expect(dialog.textContent).toContain('TSK-000001');
      (dialog.querySelector('[data-action="cancel"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(deleteSpy).not.toHaveBeenCalled();
    }));

    it('deletes after confirmation, shows a toast and reloads the list', fakeAsync(() => {
      const fixture = create('Administrator');
      const toastSpy = spyOn(TestBed.inject(ToastService), 'success');
      listSpy.calls.reset();

      (el(fixture).querySelector('tr.task-row [data-action="delete"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
      flushMicrotasks();
      tick(SEARCH_DEBOUNCE_MS);

      expect(deleteSpy).toHaveBeenCalledWith(1);
      expect(toastSpy).toHaveBeenCalledWith('TSK-000001 deleted');
      expect(listSpy).toHaveBeenCalled();
    }));

    it('shows the server message when the delete is rejected', fakeAsync(() => {
      deleteSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404, error: { error: 'Task not found.' } })));
      const fixture = create('Administrator');
      const errorSpy = spyOn(TestBed.inject(ToastService), 'error');

      (el(fixture).querySelector('tr.task-row [data-action="delete"]') as HTMLButtonElement).click();
      fixture.detectChanges();
      (el(fixture).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(errorSpy).toHaveBeenCalledWith('Task not found.');
    }));
  });

  it('renders every list column with derived deadline badges', () => {
    const fixture = create('Manager');
    const root = el(fixture);

    const headers = Array.from(root.querySelectorAll('th')).map((th) => th.textContent?.trim());
    expect(headers).toEqual(['Code', 'Title', 'Project', 'Assignee', 'Creator', 'Priority', 'Start', 'Deadline', 'Est. hours', 'Status', 'Actions']);

    const rows = root.querySelectorAll('tr.task-row');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('TSK-000001');
    expect(rows[0].textContent).toContain('Maya Manager');
    expect(rows[0].textContent).toContain('In Progress');
    expect(rows[0].querySelector('[data-stage]')?.textContent?.trim()).toBe('Upcoming');
    expect(rows[1].classList).toContain('task-row--overdue');
    expect(rows[1].querySelector('[data-stage]')?.textContent?.trim()).toBe('Overdue');
    expect(rows[1].querySelector('a')?.getAttribute('href')).toBe('/tasks/2?returnUrl=%2Ftasks');
    expect(root.textContent).toContain('1–2 of 2 tasks');
  });

  it('shows the Create task button and employee filter to Managers', () => {
    const fixture = create('Manager');

    expect(el(fixture).querySelector('[data-action="create"]')).not.toBeNull();
    expect(el(fixture).querySelector('#task-filter-assignee')).not.toBeNull();
    expect(el(fixture).querySelector('[data-action="board"]')?.getAttribute('href')).toBe('/board');
    expect(assigneesSpy).toHaveBeenCalled();
  });

  it('hides the Create task button and employee filter from Employees', () => {
    const fixture = create('Employee');

    expect(el(fixture).querySelector('[data-action="create"]')).toBeNull();
    expect(el(fixture).querySelector('#task-filter-assignee')).toBeNull();
    expect(assigneesSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('h1')?.textContent).toContain('My Tasks');
  });

  it('debounces the search before reloading', fakeAsync(() => {
    const fixture = create('Manager');
    listSpy.calls.reset();

    const input = el(fixture).querySelector<HTMLInputElement>('#task-search')!;
    input.value = 'mock';
    input.dispatchEvent(new Event('input'));
    tick(SEARCH_DEBOUNCE_MS - 1);
    expect(listSpy).not.toHaveBeenCalled();

    tick(1);
    expect(listSpy).toHaveBeenCalledTimes(1);
    expect(lastFilter()['search']).toBe('mock');
  }));

  it('applies employee, status, priority and deadline filters from page one', () => {
    const fixture = create('Manager');
    const component = fixture.componentInstance;
    component.page.set(3);

    component.setAssignee('3');
    component.setStatus('Blocked');
    component.setPriority('High');
    component.setDeadlineFrom('2026-10-01');
    component.setDeadlineTo('2026-10-31');

    expect(lastFilter()).toEqual({
      page: 1,
      pageSize: PAGE_SIZE,
      assignedToUserId: 3,
      status: 'Blocked',
      priority: 'High',
      deadlineFrom: new Date(2026, 9, 1).toISOString(),
      deadlineTo: new Date(2026, 9, 31, 23, 59, 59, 999).toISOString()
    });
  });

  it('opens pre-filtered to the employee linked from the workload screen', () => {
    create('Manager', { assignedToUserId: '3' });

    expect(lastFilter()['assignedToUserId']).toBe(3);
  });

  it('ignores an invalid assignedToUserId query parameter', () => {
    const fixture = create('Manager', { assignedToUserId: 'abc' });

    expect(fixture.componentInstance.assignedToUserId()).toBeNull();
    expect(lastFilter()['assignedToUserId']).toBeUndefined();
  });

  it('builds deadline bounds from local calendar days', () => {
    expect(startOfLocalDay('2026-10-15')).toBe(new Date(2026, 9, 15).toISOString());
    expect(endOfLocalDay('2026-10-15')).toBe(new Date(2026, 9, 15, 23, 59, 59, 999).toISOString());
  });

  it('pages through results', () => {
    listSpy.and.returnValue(of(page([item({})], 45)));
    const fixture = create('Manager');
    fixture.detectChanges();

    expect(el(fixture).textContent).toContain('Page 1 of 3');
    el(fixture).querySelector<HTMLButtonElement>('[data-action="next"]')!.click();

    expect(lastFilter()['page']).toBe(2);
  });

  it('shows the empty state when nothing matches the filters', () => {
    listSpy.and.returnValue(of(page([])));
    const fixture = create('Manager');
    fixture.componentInstance.setStatus('Cancelled');
    fixture.detectChanges();

    expect(el(fixture).textContent).toContain('No tasks match these filters');
  });

  it('shows the error state and retries', () => {
    listSpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create('Manager');
    fixture.detectChanges();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load tasks');

    listSpy.and.returnValue(of(page([item({})])));
    el(fixture).querySelector<HTMLButtonElement>('[role="alert"] button')!.click();
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('tr.task-row').length).toBe(1);
  });

  describe('dashboard drill-down', () => {
    const drillParams = {
      bucket: 'Overdue',
      departmentId: '4',
      from: '2026-09-22',
      to: '2026-10-21',
      basis: 'Deadline',
      priority: 'High'
    };

    it('applies the drill-down params to the list request and shows where it came from', () => {
      const fixture = create('Administrator', drillParams);
      fixture.detectChanges();

      expect(lastFilter()).toEqual(
        jasmine.objectContaining({
          bucket: 'Overdue',
          departmentId: 4,
          from: '2026-09-22',
          to: '2026-10-21',
          basis: 'Deadline',
          priority: 'High'
        })
      );
      expect(el(fixture).querySelector('[data-drill]')?.textContent).toContain('Opened from the dashboard');
      expect(el(fixture).querySelector<HTMLSelectElement>('#task-filter-priority')?.value).toBe('High');
    });

    it('ignores malformed drill-down params', () => {
      const fixture = create('Administrator', { bucket: 'Nonsense', departmentId: 'abc', from: 'x', to: 'y' });
      fixture.detectChanges();

      expect(el(fixture).querySelector('[data-drill]')).toBeFalsy();
      expect(lastFilter()['bucket']).toBeUndefined();
      expect(lastFilter()['departmentId']).toBeUndefined();
    });

    it('drops the dashboard filters when asked to show all tasks', () => {
      const fixture = create('Administrator', drillParams);
      fixture.detectChanges();

      el(fixture).querySelector<HTMLButtonElement>('[data-action="clear-drill"]')!.click();
      fixture.detectChanges();

      expect(el(fixture).querySelector('[data-drill]')).toBeFalsy();
      expect(lastFilter()['bucket']).toBeUndefined();
      expect(lastFilter()['departmentId']).toBeUndefined();
    });
  });
});
