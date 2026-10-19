import { HttpErrorResponse } from '@angular/common/http';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthUser, UserRole } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { TaskCard } from '../../core/models';
import { ToastService } from '../../shared/ui/toast.service';
import { KanbanComponent } from './kanban.component';
import { TaskService } from './task.service';

describe('KanbanComponent', () => {
  const todo: TaskCard = {
    id: 1,
    taskCode: 'TSK-1',
    title: 'Design screen',
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

    expect(navigateSpy).toHaveBeenCalledWith(['/tasks', 1]);
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
