import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { TaskDetails, User } from '../../core/models';
import { UserService } from '../../core/services/user.service';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskCreateComponent, toLocalInput } from './task-create.component';
import { TaskService } from './task.service';

describe('TaskCreateComponent', () => {
  function user(overrides: Partial<User>): User {
    return {
      id: 3,
      employeeCode: 'EMP-003',
      fullName: 'Joe Thomas',
      username: 'joe',
      email: 'joe@taskflow.local',
      role: 'Employee',
      isActive: true,
      createdAt: '2026-09-01T00:00:00Z',
      ...overrides
    };
  }

  const users: User[] = [
    user({}),
    user({ id: 4, fullName: 'Ann Varghese', username: 'ann', employeeCode: 'EMP-004' }),
    user({ id: 9, fullName: 'Maya Manager', username: 'maya', role: 'Manager' })
  ];

  let createSpy: jasmine.Spy;
  let getAllSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;
  let navigateSpy: jasmine.Spy;

  function create(): ComponentFixture<TaskCreateComponent> {
    TestBed.configureTestingModule({
      imports: [TaskCreateComponent],
      providers: [
        provideRouter([]),
        { provide: TaskService, useValue: { create: createSpy } },
        { provide: UserService, useValue: { getAll: getAllSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });
    navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(TaskCreateComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<TaskCreateComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function error(fixture: ComponentFixture<TaskCreateComponent>, field: string): string | null {
    return el(fixture).querySelector(`[data-error="${field}"]`)?.textContent?.trim() ?? null;
  }

  function submit(fixture: ComponentFixture<TaskCreateComponent>): void {
    el(fixture).querySelector<HTMLButtonElement>('[data-action="create"]')!.click();
    fixture.detectChanges();
  }

  function fillValid(fixture: ComponentFixture<TaskCreateComponent>): void {
    fixture.componentInstance.form.patchValue({
      title: '  Prepare launch plan  ',
      description: '',
      assignedToUserId: 3,
      priority: 'High',
      startDate: '2026-10-14T09:00',
      deadline: '2026-10-17T17:00',
      estimatedHours: 6
    });
    fixture.detectChanges();
  }

  beforeEach(() => {
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(users));
    createSpy = jasmine.createSpy('create');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  it('loads active users and offers only employees as assignees', () => {
    const fixture = create();

    expect(getAllSpy).toHaveBeenCalledWith({ isActive: true });
    const options = Array.from(el(fixture).querySelectorAll('#task-assignee option')).map((option) => option.textContent?.trim());
    expect(options).toEqual(['Select an employee…', 'Joe Thomas (EMP-003)', 'Ann Varghese (EMP-004)']);
  });

  it('defaults the priority, dates and reminder options', () => {
    const fixture = create();
    const value = fixture.componentInstance.form.getRawValue();

    expect(value.priority).toBe('Medium');
    expect(value.firstReminderDays).toBe(2);
    expect(value.secondReminderDays).toBe(1);
    expect(value.overdueReminderFrequency).toBe('Daily');
    expect(new Date(value.deadline).getTime()).toBeGreaterThan(new Date(value.startDate).getTime());
  });

  it('shows an error for each missing required field and does not submit', () => {
    const fixture = create();
    fixture.componentInstance.form.patchValue({ startDate: '', deadline: '' });

    submit(fixture);

    expect(error(fixture, 'title')).toBe('Title is required.');
    expect(error(fixture, 'assignedToUserId')).toBe('Select the employee to assign.');
    expect(error(fixture, 'startDate')).toBe('Start date is required.');
    expect(error(fixture, 'deadline')).toBe('Deadline is required.');
    expect(error(fixture, 'estimatedHours')).toBe('Estimated hours are required.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('treats a whitespace-only title as missing', () => {
    const fixture = create();
    fillValid(fixture);
    fixture.componentInstance.form.patchValue({ title: '   ' });

    submit(fixture);

    expect(error(fixture, 'title')).toBe('Title is required.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('rejects a deadline that is not after the start date', () => {
    const fixture = create();
    fillValid(fixture);
    fixture.componentInstance.form.patchValue({ startDate: '2026-10-14T09:00', deadline: '2026-10-14T09:00' });

    submit(fixture);

    expect(error(fixture, 'deadline')).toBe('Deadline must be after the start date.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('rejects zero or negative estimated hours', () => {
    const fixture = create();
    fillValid(fixture);
    fixture.componentInstance.form.patchValue({ estimatedHours: 0 });

    submit(fixture);

    expect(error(fixture, 'estimatedHours')).toBe('Estimated hours must be a positive number.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('rejects a title longer than 200 characters', () => {
    const fixture = create();
    fillValid(fixture);
    fixture.componentInstance.form.patchValue({ title: 'x'.repeat(201) });

    submit(fixture);

    expect(error(fixture, 'title')).toBe('Title must be 200 characters or fewer.');
  });

  it('excludes the assignee from the recipient choices', () => {
    const fixture = create();
    fillValid(fixture);

    const recipients = Array.from(el(fixture).querySelectorAll('[data-recipient]')).map((input) =>
      Number(input.getAttribute('data-recipient'))
    );
    expect(recipients).toEqual([4, 9]);
  });

  it('submits the trimmed request, toasts and opens the created task', () => {
    createSpy.and.returnValue(of({ id: 42, taskCode: 'TSK-000042', assignedToName: 'Joe Thomas' } as TaskDetails));
    const fixture = create();
    fillValid(fixture);
    fixture.componentInstance.form.patchValue({ firstReminderDays: 3, overdueReminderFrequency: 'Weekly' });
    el(fixture).querySelector<HTMLInputElement>('[data-recipient="9"]')!.click();
    fixture.detectChanges();

    submit(fixture);

    expect(createSpy).toHaveBeenCalledOnceWith({
      title: 'Prepare launch plan',
      description: null,
      assignedToUserId: 3,
      priority: 'High',
      startDate: new Date('2026-10-14T09:00').toISOString(),
      deadline: new Date('2026-10-17T17:00').toISOString(),
      estimatedHours: 6,
      additionalRecipientUserIds: [9],
      firstReminderDays: 3,
      secondReminderDays: 1,
      overdueReminderFrequency: 'Weekly'
    });
    expect(toast.success).toHaveBeenCalledWith('TSK-000042 created and assigned to Joe Thomas');
    expect(navigateSpy).toHaveBeenCalledWith(['/tasks', 42]);
  });

  it('shows the server error and stays on the form', () => {
    createSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { message: 'The assigned employee does not exist or is not active.' } }))
    );
    const fixture = create();
    fillValid(fixture);

    submit(fixture);

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('not active');
    expect(navigateSpy).not.toHaveBeenCalled();
    expect(fixture.componentInstance.saving()).toBeFalse();
  });

  it('formats datetime-local values in local time', () => {
    expect(toLocalInput(new Date(2026, 0, 5, 7, 3))).toBe('2026-01-05T07:03');
  });
});
