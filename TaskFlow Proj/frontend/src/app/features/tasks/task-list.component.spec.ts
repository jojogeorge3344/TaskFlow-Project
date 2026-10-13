import { signal } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AuthUser, PagedResult, TaskListItem, UserRole } from '../../core/models';
import { PAGE_SIZE, SEARCH_DEBOUNCE_MS, TaskListComponent } from './task-list.component';
import { TaskService } from './task.service';

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
      ...overrides
    };
  }

  function page(items: TaskListItem[], total = items.length, pageNumber = 1): PagedResult<TaskListItem> {
    return { items, total, page: pageNumber, pageSize: PAGE_SIZE };
  }

  let listSpy: jasmine.Spy;
  let assigneesSpy: jasmine.Spy;

  function create(role: UserRole): ComponentFixture<TaskListComponent> {
    const user: AuthUser = { id: 1, fullName: 'Test', username: 'test', email: 't@x', role, employeeCode: null };
    TestBed.configureTestingModule({
      imports: [TaskListComponent],
      providers: [
        provideRouter([]),
        { provide: TaskService, useValue: { list: listSpy, getAssignees: assigneesSpy } },
        { provide: AuthService, useValue: { currentUser: signal(user).asReadonly() } }
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
  });

  it('renders every list column with derived deadline badges', () => {
    const fixture = create('Manager');
    const root = el(fixture);

    const headers = Array.from(root.querySelectorAll('th')).map((th) => th.textContent?.trim());
    expect(headers).toEqual(['Code', 'Title', 'Assignee', 'Creator', 'Priority', 'Start', 'Deadline', 'Est. hours', 'Status']);

    const rows = root.querySelectorAll('tr.task-row');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('TSK-000001');
    expect(rows[0].textContent).toContain('Maya Manager');
    expect(rows[0].textContent).toContain('In Progress');
    expect(rows[0].querySelector('[data-stage]')?.textContent?.trim()).toBe('Upcoming');
    expect(rows[1].classList).toContain('task-row--overdue');
    expect(rows[1].querySelector('[data-stage]')?.textContent?.trim()).toBe('Overdue');
    expect(rows[1].querySelector('a')?.getAttribute('href')).toBe('/tasks/2');
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
      deadlineFrom: '2026-10-01T00:00:00Z',
      deadlineTo: '2026-10-31T23:59:59Z'
    });
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
});
