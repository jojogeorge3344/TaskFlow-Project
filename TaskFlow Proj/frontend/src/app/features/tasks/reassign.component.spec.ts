import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TaskDetails, User } from '../../core/models';
import { UserService } from '../../core/services/user.service';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskReassignComponent } from './reassign.component';
import { TaskService } from './task.service';

describe('TaskReassignComponent', () => {
  const task: TaskDetails = {
    id: 7,
    taskCode: 'TSK-1007',
    title: 'Integrate payments',
    description: null,
    assignedToUserId: 3,
    assignedToName: 'Joe',
    createdByUserId: 1,
    createdByName: 'Boss',
    priority: 'High',
    startDate: '2026-10-01T08:00:00Z',
    deadline: '2026-10-20T10:00:00Z',
    estimatedHours: 8,
    status: 'InProgress',
    stage: 'Upcoming',
    recipients: [],
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z'
  };

  const employees: User[] = [
    { id: 3, fullName: 'Joe', username: 'joe', email: 'joe@taskflow.local', role: 'Employee', isActive: true, createdAt: '2026-09-01' },
    { id: 9, fullName: 'Sara Dev', username: 'sara', email: 'sara@taskflow.local', role: 'Employee', isActive: true, createdAt: '2026-09-01' },
    { id: 11, fullName: 'Old Ten', username: 'old', email: 'old@taskflow.local', role: 'Employee', isActive: false, createdAt: '2026-09-01' }
  ];

  let fixture: ComponentFixture<TaskReassignComponent>;
  let component: TaskReassignComponent;
  let getByIdSpy: jasmine.Spy;
  let getAllSpy: jasmine.Spy;
  let reassignSpy: jasmine.Spy;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };
  let navigateSpy: jasmine.Spy;

  beforeEach(() => {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(task));
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(employees));
    reassignSpy = jasmine.createSpy('reassign');
    toast = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    navigateSpy = jasmine.createSpy('navigate');

    TestBed.configureTestingModule({
      imports: [TaskReassignComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: TaskService, useValue: { getById: getByIdSpy, reassign: reassignSpy } },
        { provide: UserService, useValue: { getAll: getAllSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });

    navigateSpy = spyOn(TestBed.inject(Router), 'navigate');

    fixture = TestBed.createComponent(TaskReassignComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the task and only lists active employees other than the current assignee', () => {
    expect(getByIdSpy).toHaveBeenCalledWith(7);
    expect(getAllSpy).toHaveBeenCalledWith({ role: 'Employee', isActive: true });

    const ids = component.eligibleEmployees().map((employee) => employee.id);
    expect(ids).toEqual([9]);
  });

  it('enables the submit action only when an assignee and reason are provided', () => {
    expect(component.canSubmit()).toBeFalse();

    component.selectedUserId.set(9);
    expect(component.canSubmit()).toBeFalse();

    component.reason.set('Handing over');
    expect(component.canSubmit()).toBeTrue();
  });

  it('asks for confirmation and reassigns when confirmed', async () => {
    reassignSpy.and.returnValue(of(task));
    const dialog = fixture.debugElement.query(By.css('tf-confirm-dialog')).componentInstance as ConfirmDialogComponent;
    spyOn(dialog, 'open').and.returnValue(Promise.resolve(true));

    component.selectedUserId.set(9);
    component.reason.set('Handing over');

    await component.confirm();

    expect(dialog.open).toHaveBeenCalled();
    expect(reassignSpy).toHaveBeenCalledWith(7, { newAssigneeUserId: 9, reason: 'Handing over' });
    expect(navigateSpy).toHaveBeenCalledWith(['/tasks', 7]);
    expect(toast.success).toHaveBeenCalled();
  });

  it('does not reassign when the confirmation is declined', async () => {
    const dialog = fixture.debugElement.query(By.css('tf-confirm-dialog')).componentInstance as ConfirmDialogComponent;
    spyOn(dialog, 'open').and.returnValue(Promise.resolve(false));

    component.selectedUserId.set(9);
    component.reason.set('Handing over');

    await component.confirm();

    expect(reassignSpy).not.toHaveBeenCalled();
  });

  it('toasts a failure when reassignment is rejected', async () => {
    reassignSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, statusText: 'Bad Request', error: { detail: 'A reassignment reason is required.' } }))
    );
    const dialog = fixture.debugElement.query(By.css('tf-confirm-dialog')).componentInstance as ConfirmDialogComponent;
    spyOn(dialog, 'open').and.returnValue(Promise.resolve(true));

    component.selectedUserId.set(9);
    component.reason.set('Why');

    await component.confirm();

    expect(toast.error).toHaveBeenCalledWith('A reassignment reason is required.');
    expect(navigateSpy).not.toHaveBeenCalled();
  });

  it('navigates back without reassigning on cancel', async () => {
    const dialog = fixture.debugElement.query(By.css('tf-confirm-dialog')).componentInstance as ConfirmDialogComponent;
    const openSpy = spyOn(dialog, 'open');

    await component.cancel();

    expect(openSpy).not.toHaveBeenCalled();
    expect(navigateSpy).toHaveBeenCalledWith(['/tasks', 7]);
  });
});