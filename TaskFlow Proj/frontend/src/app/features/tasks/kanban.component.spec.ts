import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
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

  let fixture: ComponentFixture<KanbanComponent>;
  let component: KanbanComponent;
  let getBoardSpy: jasmine.Spy;
  let changeStatusSpy: jasmine.Spy;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getBoardSpy = jasmine.createSpy('getBoard').and.returnValue(of([todo, inProgress, completed]));
    changeStatusSpy = jasmine.createSpy('changeStatus');
    toast = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    navigateSpy = jasmine.createSpy('navigate');

    TestBed.configureTestingModule({
      imports: [KanbanComponent],
      providers: [
        { provide: TaskService, useValue: { getBoard: getBoardSpy, changeStatus: changeStatusSpy } },
        { provide: ToastService, useValue: toast },
        { provide: Router, useValue: { navigate: navigateSpy } }
      ]
    });

    fixture = TestBed.createComponent(KanbanComponent);
    component = fixture.componentInstance;
  });

  it('groups board tasks into the four board columns', () => {
    fixture.detectChanges();

    const columns = component.columns();
    expect(columns.map((column) => column.status)).toEqual(['ToDo', 'InProgress', 'Blocked', 'Completed']);
    expect(columns[0].tasks.map((task) => task.id)).toEqual([1]);
    expect(columns[1].tasks.map((task) => task.id)).toEqual([2]);
    expect(columns[2].tasks).toEqual([]);
    expect(columns[3].tasks.map((task) => task.id)).toEqual([3]);
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
    const targetTasks = component.columns()[3].tasks;
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
    expect(component.columns()[3].tasks.map((task) => task.id)).toEqual([1, 3]);
    expect(toast.success).toHaveBeenCalled();
  });

  it('reorders cards within the same column without a status change', () => {
    changeStatusSpy.and.returnValue(of({}));
    fixture.detectChanges();

    const sourceTasks = component.columns()[3].tasks;
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
    const targetTasks = component.columns()[3].tasks;
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
    expect(component.columns()[3].tasks.map((task) => task.id)).toEqual([3]);
    expect(toast.error).toHaveBeenCalled();
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('shows an error state when the board cannot be loaded', () => {
    getBoardSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500, statusText: 'Server Error' })));

    fixture.detectChanges();

    expect(component.error()).not.toBeNull();
    expect(component.loading()).toBeFalse();
  });
});