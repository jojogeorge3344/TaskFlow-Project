import { HttpErrorResponse } from '@angular/common/http';
import { fakeAuthService, systemPermissions } from '../../../../shared/testing/permissions';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthUser, UserRole } from '../../../../shared/auth/auth.models';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { TaskCard } from '../../task-management/models/task.model';
import { ProjectService } from '../../../management/project-management/services/project.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { KanbanComponent } from './kanban.component';
import { TaskService } from '../../task-management/services/task.service';

describe('KanbanComponent', () => {
  const todo: TaskCard = {
    id: 1,
    taskCode: 'TSK-1',
    title: 'Design screen',
    projectId: 10,
    projectName: 'Atlas',
    assignedToUserId: 3,
    assignedToName: 'Joe',
    priority: 'High',
    deadline: '2026-10-20T10:00:00Z',
    status: 'ToDo',
    isOverdue: false
  };
  const inProgress: TaskCard = {
    id: 2,
    taskCode: 'TSK-2',
    title: 'Build API',
    projectId: 11,
    projectName: 'Beacon',
    assignedToUserId: 3,
    assignedToName: 'Joe',
    priority: 'Medium',
    deadline: '2026-10-22T10:00:00Z',
    status: 'InProgress',
    isOverdue: true
  };
  const completed: TaskCard = {
    id: 3,
    taskCode: 'TSK-3',
    title: 'Ship release',
    projectId: 10,
    projectName: 'Atlas',
    assignedToUserId: 3,
    assignedToName: 'Joe',
    priority: 'Low',
    deadline: '2026-10-18T10:00:00Z',
    status: 'Completed',
    isOverdue: false
  };
  const otherAssignee: TaskCard = {
    ...inProgress,
    id: 4,
    taskCode: 'TSK-4',
    title: 'Write docs',
    assignedToUserId: 4,
    assignedToName: 'Ann',
    status: 'ToDo',
    deadline: '2026-10-25T10:00:00Z'
  };

  const userSignal = signal<AuthUser | null>(null);
  let fixture: ComponentFixture<KanbanComponent>;
  let component: KanbanComponent;
  let getBoardSpy: jasmine.Spy;
  let getAssigneesSpy: jasmine.Spy;
  let changeStatusSpy: jasmine.Spy;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };
  let navigateSpy: jasmine.Spy;

  function asUser(role: UserRole): void {
    userSignal.set({
      id: 1,
      fullName: 'Test User',
      username: 'test',
      email: 'test@taskflow.com',
      role,
      employeeCode: null,
      permissions: systemPermissions(role)
    });
  }

  beforeEach(() => {
    userSignal.set(null);
    getBoardSpy = jasmine.createSpy('getBoard').and.returnValue(of([todo, inProgress, completed]));
    getAssigneesSpy = jasmine
      .createSpy('getAssignees')
      .and.returnValue(
        of([
          { userId: 3, fullName: 'Joe Thomas' },
          { userId: 4, fullName: 'Ann Mary' }
        ])
      );
    changeStatusSpy = jasmine.createSpy('changeStatus');
    toast = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    navigateSpy = jasmine.createSpy('navigate');

    TestBed.configureTestingModule({
      imports: [KanbanComponent],
      providers: [
        {
          provide: TaskService,
          useValue: { getBoard: getBoardSpy, getAssignees: getAssigneesSpy, changeStatus: changeStatusSpy }
        },
        {
          provide: ProjectService,
          useValue: {
            getAll: () =>
              of([
                { id: 10, name: 'Atlas', createdAt: '2026-01-01T00:00:00Z', employeeCount: 2, managers: [] },
                { id: 11, name: 'Beacon', createdAt: '2026-01-01T00:00:00Z', employeeCount: 1, managers: [] }
              ])
          }
        },
        { provide: ToastService, useValue: toast },
        { provide: Router, useValue: { navigate: navigateSpy } },
        { provide: AuthService, useValue: fakeAuthService(userSignal.asReadonly()) }
      ]
    });

    fixture = TestBed.createComponent(KanbanComponent);
    component = fixture.componentInstance;
  });

  it('groups board tasks into the five board columns', () => {
    fixture.detectChanges();

    const columns = component.columns();
    expect(columns.map((column) => column.status)).toEqual(['ToDo', 'InProgress', 'InQA', 'Blocked', 'Completed']);
    expect(columns[0].tasks.map((task) => task.id)).toEqual([1]);
    expect(columns[1].tasks.map((task) => task.id)).toEqual([2]);
    expect(columns[2].tasks).toEqual([]);
    expect(columns[3].tasks).toEqual([]);
    expect(columns[4].tasks.map((task) => task.id)).toEqual([3]);
  });

  it('navigates to the task details on card click', () => {
    fixture.detectChanges();

    component.open(todo);

    expect(navigateSpy).toHaveBeenCalledWith(['/tasks', 1], { queryParams: { returnUrl: '/board' } });
  });

  it('dispatches a status change when a card is dropped on another column', () => {
    changeStatusSpy.and.returnValue(of({}));
    fixture.detectChanges();

    const sourceTasks = component.columns()[0].tasks;
    const targetTasks = component.columns()[4].tasks;
    const fakeEvent = {
      previousContainer: { data: sourceTasks },
      container: { data: targetTasks },
      previousIndex: 0,
      currentIndex: 0,
      item: { data: todo }
    } as unknown as CdkDragDrop<TaskCard[]>;

    component.drop(fakeEvent, 'Completed');

    expect(changeStatusSpy).toHaveBeenCalledWith(1, { status: 'Completed' });
    expect(component.columns()[0].tasks).toEqual([]);
    expect(component.columns()[4].tasks.map((task) => task.id)).toEqual([1, 3]);
    expect(toast.success).toHaveBeenCalled();
  });

  it('reorders cards within the same column without a status change', () => {
    changeStatusSpy.and.returnValue(of({}));
    fixture.detectChanges();

    const sourceTasks = component.columns()[4].tasks;
    const fakeEvent = {
      previousContainer: { data: sourceTasks },
      container: { data: sourceTasks },
      previousIndex: 0,
      currentIndex: 0,
      item: { data: completed }
    } as unknown as CdkDragDrop<TaskCard[]>;

    component.drop(fakeEvent, 'Completed');

    expect(changeStatusSpy).not.toHaveBeenCalled();
  });

  it('rolls the card back when the status change returns a 4xx', () => {
    changeStatusSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, statusText: 'Bad Request' }))
    );
    fixture.detectChanges();

    const sourceTasks = component.columns()[0].tasks;
    const targetTasks = component.columns()[4].tasks;
    const fakeEvent = {
      previousContainer: { data: sourceTasks },
      container: { data: targetTasks },
      previousIndex: 0,
      currentIndex: 0,
      item: { data: todo }
    } as unknown as CdkDragDrop<TaskCard[]>;

    component.drop(fakeEvent, 'Completed');

    // The card is restored to its original column.
    expect(component.columns()[0].tasks.map((task) => task.id)).toEqual([1]);
    expect(component.columns()[4].tasks.map((task) => task.id)).toEqual([3]);
    expect(toast.error).toHaveBeenCalled();
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('shows an error state when the board cannot be loaded', () => {
    getBoardSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Server Error' })));

    fixture.detectChanges();

    expect(component.error()).not.toBeNull();
    expect(component.loading()).toBeFalse();
  });

  describe('board permissions', () => {
    const filterRow = () => (fixture.nativeElement as HTMLElement).querySelector('.kanban__filters');

    it('shows the filter row to a user who holds board.filters.use', () => {
      asUser('Employee');
      fixture.detectChanges();

      expect(component.canFilter()).toBeTrue();
      expect(filterRow()).not.toBeNull();
    });

    it('hides the filter row but keeps the cards without board.filters.use', () => {
      asUser('Employee');
      userSignal.update((user) => ({ ...user!, permissions: user!.permissions.filter((key) => key !== Permissions.BoardFilters) }));
      fixture.detectChanges();

      expect(component.canFilter()).toBeFalse();
      expect(filterRow()).toBeNull();
      expect(component.columns().flatMap((column) => column.tasks).length).toBeGreaterThan(0);
    });
  });

  describe('filters', () => {
    beforeEach(() => {
      asUser('Manager');
      getBoardSpy.and.returnValue(of([todo, inProgress, completed, otherAssignee]));
      fixture.detectChanges();
    });

    it('narrows the board by assignee', () => {
      component.setAssignee('4');
      fixture.detectChanges();

      const matched = component.columns().flatMap((column) => column.tasks);
      expect(matched.map((task) => task.id)).toEqual([4]);
      expect(el().querySelectorAll('.kanban__card').length).toBe(1);
    });

    it('offers every project with "All projects" as the default', () => {
      const select = el().querySelector<HTMLSelectElement>('#board-filter-project')!;
      const labels = Array.from(select.options).map((option) => option.textContent!.trim());

      expect(labels).toEqual(['All projects', 'Atlas', 'Beacon']);
      expect(component.projectId()).toBeNull();
    });

    it('narrows the board by project', () => {
      component.setProject('10');
      fixture.detectChanges();

      const matched = component.columns().flatMap((column) => column.tasks);
      expect(matched.map((task) => task.id).sort()).toEqual([1, 3]);
    });

    it('combines the project filter with status', () => {
      component.setProject('10');
      component.setStatus('Completed');
      fixture.detectChanges();

      const matched = component.columns().flatMap((column) => column.tasks);
      expect(matched.map((task) => task.id)).toEqual([3]);
    });

    it('resets the project filter with Clear', () => {
      component.setProject('11');
      expect(component.hasFilters()).toBeTrue();

      component.clearFilters();
      fixture.detectChanges();

      expect(component.projectId()).toBeNull();
      expect(component.hasFilters()).toBeFalse();
    });

    it('shows the filtered-empty state for a project with no visible tasks', () => {
      component.setProject('99');
      fixture.detectChanges();

      expect(component.filteredEmpty()).toBeTrue();
      expect(el().querySelector('[data-testid="board-filtered-empty"]')).not.toBeNull();
    });

    it('shows the project name on each card', () => {
      const names = Array.from(el().querySelectorAll('[data-field="card-project"]')).map((node) =>
        node.textContent!.trim()
      );

      expect(names.sort()).toEqual(['Atlas', 'Atlas', 'Beacon', 'Beacon']);
    });

    it('narrows the board by status', () => {
      component.setStatus('Completed');
      fixture.detectChanges();

      const nonEmpty = component.columns().filter((column) => column.tasks.length > 0);
      expect(nonEmpty.length).toBe(1);
      expect(nonEmpty[0].status).toBe('Completed');
      expect(nonEmpty[0].tasks.map((task) => task.id)).toEqual([3]);
    });

    it('narrows the board by an inclusive deadline range', () => {
      component.setDeadlineFrom('2026-10-20');
      component.setDeadlineTo('2026-10-22');
      fixture.detectChanges();

      const matched = component.columns().flatMap((column) => column.tasks);
      expect(matched.map((task) => task.id)).toEqual([1, 2]);
    });

    it('combines filters with AND semantics', () => {
      component.setAssignee('3');
      component.setStatus('ToDo');
      fixture.detectChanges();

      const matched = component.columns().flatMap((column) => column.tasks);
      expect(matched.map((task) => task.id)).toEqual([1]);
    });

    it('clears every filter and restores the full board', () => {
      component.setAssignee('4');
      component.setStatus('Blocked');
      expect(component.hasFilters()).toBeTrue();

      component.clearFilters();
      fixture.detectChanges();

      expect(component.hasFilters()).toBeFalse();
      const matched = component.columns().flatMap((column) => column.tasks);
      expect(matched.map((task) => task.id)).toEqual([1, 4, 2, 3]);
    });

    it('shows a filtered-empty state when nothing matches', () => {
      component.setAssignee('99');
      fixture.detectChanges();

      expect(component.filteredEmpty()).toBeTrue();
      const empty = el().querySelector('[data-testid="board-filtered-empty"]');
      expect(empty).not.toBeNull();
      expect(empty!.textContent).toContain('No tasks match the current filters');
      expect(el().querySelectorAll('.kanban__card').length).toBe(0);
    });

    it('still commits a drag-drop status change while filtered', () => {
      changeStatusSpy.and.returnValue(of({}));
      component.setStatus('ToDo');

      const sourceTasks = component.columns()[0].tasks;
      const targetTasks = component.columns()[1].tasks;
      const fakeEvent = {
        previousContainer: { data: sourceTasks },
        container: { data: targetTasks },
        previousIndex: 0,
        currentIndex: 0,
        item: { data: todo }
      } as unknown as CdkDragDrop<TaskCard[]>;

      component.drop(fakeEvent, 'InProgress');

      expect(changeStatusSpy).toHaveBeenCalledWith(1, { status: 'InProgress' });
      // The card left the filtered To Do column; with a status filter active it no longer matches.
      expect(component.columns()[0].tasks.map((task) => task.id)).not.toContain(1);
    });
  });

  it('offers the assignee filter and loads assignees for Managers', () => {
    asUser('Manager');
    fixture.detectChanges();

    expect(getAssigneesSpy).toHaveBeenCalled();
    const options = Array.from(el().querySelectorAll('#board-filter-assignee option')).map(
      (option) => option.textContent?.trim()
    );
    expect(options).toEqual(['All assignees', 'Joe Thomas', 'Ann Mary']);
  });

  it('hides the assignee filter and skips assignees for Employees', () => {
    asUser('Employee');
    fixture.detectChanges();

    expect(getAssigneesSpy).not.toHaveBeenCalled();
    expect(el().querySelector('#board-filter-assignee')).toBeNull();
    expect(el().querySelector('#board-filter-status')).not.toBeNull();
    expect(el().querySelector('#board-filter-from')).not.toBeNull();
    expect(el().querySelector('#board-filter-to')).not.toBeNull();
  });

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }
});
