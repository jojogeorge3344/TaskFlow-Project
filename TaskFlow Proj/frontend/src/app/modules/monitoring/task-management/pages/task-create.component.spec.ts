import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { ReminderSettings } from '../../../settings/models/settings.model';
import { AssigneeCandidate, TaskDetails } from '../models/task.model';
import { User } from '../../../management/employee-management/models/user.model';
import { ApplicationSettingsService } from '../../../settings/application-settings/services/application-settings.service';
import { ProjectService } from '../../../management/project-management/services/project.service';
import { ReminderSettingsService } from '../../../settings/reminder-settings/services/reminder-settings.service';
import { UserService } from '../../../management/employee-management/services/user.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TaskCreateComponent, candidateLabel, toLocalInput } from './task-create.component';
import { TaskService } from '../services/task.service';
import { NO_ALLOCATIONS, withProject } from '../../../../shared/testing/users';

describe('TaskCreateComponent', () => {
  function user(overrides: Partial<User>): User {
    return {
      id: 3,
      employeeCode: 'EMP-003',
      fullName: 'Joe Thomas',
      username: 'joe',
      email: 'joe@taskflow.com',
      role: 'Employee',
      roleId: 3,
      isActive: true,
      createdAt: '2026-09-01T00:00:00Z',
      ...NO_ALLOCATIONS,
      ...overrides
    };
  }

  function settings(overrides: Partial<ReminderSettings>): ReminderSettings {
    return {
      enabled: true,
      deadlineDayEnabled: true,
      defaultFirstDays: 2,
      defaultSecondDays: 1,
      defaultOverdueFrequency: 'Daily',
      mailEnabled: false,
      timezone: 'Asia/Kolkata',
      ...overrides
    };
  }

  const users: User[] = [
    user({}),
    user({ id: 4, fullName: 'Ann Varghese', username: 'ann', employeeCode: 'EMP-004' }),
    user({ id: 9, fullName: 'Maya Manager', username: 'maya', role: 'Manager' })
  ];

  const projects = [{ id: 5, name: 'Apollo', createdAt: '2026-09-01T00:00:00Z', employeeCount: 2, managers: [] }];

  function candidate(overrides: Partial<AssigneeCandidate>): AssigneeCandidate {
    return { userId: 3, fullName: 'Joe Thomas', employeeCode: 'EMP-003', activeTasks: 1, overdueTasks: 0, onTimeRate: null, ...overrides };
  }

  /** Apollo's team in the server's pick order: Ann is the least loaded. */
  const team: AssigneeCandidate[] = [
    candidate({ userId: 4, fullName: 'Ann Varghese', employeeCode: 'EMP-004', activeTasks: 1 }),
    candidate({ userId: 3, activeTasks: 3, overdueTasks: 1 })
  ];

  let createSpy: jasmine.Spy;
  let candidatesSpy: jasmine.Spy;
  let getByIdSpy: jasmine.Spy;
  let getAllSpy: jasmine.Spy;
  let getSettingsSpy: jasmine.Spy;
  let getAppSettingsSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;
  let navigateSpy: jasmine.Spy;

  function create(queryParams: Record<string, string> = {}): ComponentFixture<TaskCreateComponent> {
    TestBed.configureTestingModule({
      imports: [TaskCreateComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(queryParams) } } },
        { provide: TaskService, useValue: { create: createSpy, getById: getByIdSpy, getAssigneeCandidates: candidatesSpy } },
        { provide: UserService, useValue: { getAll: getAllSpy } },
        { provide: ProjectService, useValue: { getAll: () => of(projects) } },
        { provide: ReminderSettingsService, useValue: { get: getSettingsSpy } },
        {
          provide: ApplicationSettingsService,
          useValue: {
            get: getAppSettingsSpy
          }
        },
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
      projectId: 5,
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
    getSettingsSpy = jasmine.createSpy('get').and.returnValue(of(settings({})));
    getAppSettingsSpy = jasmine
      .createSpy('get')
      .and.returnValue(of({ name: 'TaskFlow', timezone: 'UTC', defaultPriority: 'Medium', inAppNotificationsEnabled: true }));
    createSpy = jasmine.createSpy('create');
    candidatesSpy = jasmine.createSpy('getAssigneeCandidates').and.returnValue(of(team));
    getByIdSpy = jasmine.createSpy('getById');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
  });

  describe('copying a task', () => {
    const source: TaskDetails = {
      id: 42,
      taskCode: 'TSK-0042',
      title: 'Prepare launch plan',
      description: 'Cover the rollout.',
      assignedToUserId: 3,
      assignedToName: 'Joe Thomas',
      createdByUserId: 9,
      createdByName: 'Maya Manager',
      priority: 'Critical',
      startDate: '2020-01-01T09:00:00Z',
      deadline: '2020-01-05T09:00:00Z',
      estimatedHours: 12,
      status: 'Completed',
      stage: 'Completed',
      recipients: [
        { userId: 3, fullName: 'Joe Thomas' },
        { userId: 9, fullName: 'Maya Manager' },
        { userId: 77, fullName: 'Gone Away' }
      ],
      createdAt: '2020-01-01T09:00:00Z',
      updatedAt: '2020-01-01T09:00:00Z',
      projectId: 5,
      projectName: 'Apollo',
      firstReminderDays: 7,
      secondReminderDays: 3,
      overdueReminderFrequency: 'Weekly'
    };

    it('pre-fills the form from the source task without creating anything', () => {
      getByIdSpy.and.returnValue(of(source));
      const fixture = create({ copyFrom: '42' });

      expect(getByIdSpy).toHaveBeenCalledWith(42);
      const value = fixture.componentInstance.form.getRawValue();
      expect(value.title).toBe('Prepare launch plan');
      expect(value.description).toBe('Cover the rollout.');
      expect(value.projectId).toBe(5);
      expect(value.assignedToUserId).toBe(3);
      expect(value.priority).toBe('Critical');
      expect(value.estimatedHours).toBe(12);
      expect(value.firstReminderDays).toBe(7);
      expect(value.secondReminderDays).toBe(3);
      expect(value.overdueReminderFrequency).toBe('Weekly');
      expect(fixture.componentInstance.recipientIds()).toEqual([9]);
      expect(createSpy).not.toHaveBeenCalled();
      expect(el(fixture).querySelector('[data-copy-notice]')?.textContent).toContain('TSK-0042');
    });

    it('keeps the new-task date defaults instead of the old dates', () => {
      getByIdSpy.and.returnValue(of(source));
      const fixture = create({ copyFrom: '42' });

      const { startDate, deadline } = fixture.componentInstance.form.getRawValue();
      expect(new Date(startDate).getFullYear()).toBeGreaterThan(2020);
      expect(new Date(deadline).getTime()).toBeGreaterThan(new Date(startDate).getTime());
    });

    it('sends the copied reminder settings when submitted', () => {
      getByIdSpy.and.returnValue(of(source));
      createSpy.and.returnValue(of({ id: 43, taskCode: 'TSK-0043', assignedToName: 'Joe Thomas' }));
      const fixture = create({ copyFrom: '42' });

      submit(fixture);

      const request = createSpy.calls.mostRecent().args[0];
      expect(request.firstReminderDays).toBe(7);
      expect(request.projectId).toBe(5);
      expect(request.assignedToUserId).toBe(3);
    });

    it('keeps the copied assignee over the least-loaded team member', () => {
      getByIdSpy.and.returnValue(of(source));
      const fixture = create({ copyFrom: '42' });

      expect(candidatesSpy).toHaveBeenCalledWith(5);
      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(3);
      expect(el(fixture).querySelector('[data-assignee-notice]')).toBeNull();
    });

    it('picks the least-loaded team member when the original is no longer on the team, and says so', () => {
      getByIdSpy.and.returnValue(of({ ...source, assignedToUserId: 99, assignedToName: 'Gone Away' }));
      const fixture = create({ copyFrom: '42' });

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(4);
      const copyNotice = el(fixture).querySelector('[data-copy-notice]')?.textContent;
      expect(copyNotice).toContain('Gone Away is no longer on this project');
      expect(copyNotice).toContain('least-loaded team member was selected');
    });

    it('leaves the assignee empty when the original is gone and the project has no team', () => {
      candidatesSpy.and.returnValue(of([]));
      getByIdSpy.and.returnValue(of({ ...source, assignedToUserId: 99, assignedToName: 'Gone Away' }));
      const fixture = create({ copyFrom: '42' });

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBeNull();
      expect(el(fixture).querySelector('[data-copy-notice]')?.textContent).toContain('can no longer be assigned');
    });

    it('applies the copied assignee when the team for a URL project arrived first', () => {
      const source$ = new Subject<TaskDetails>();
      getByIdSpy.and.returnValue(source$);
      const fixture = create({ copyFrom: '42', projectId: '5' });
      // The team is in, but the copy is still loading, so nobody is auto-picked yet.
      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBeNull();

      source$.next(source);
      fixture.detectChanges();

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(3);
    });

    it('opens an empty form with a message when the source cannot be loaded', () => {
      getByIdSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      const fixture = create({ copyFrom: '42' });

      expect(fixture.componentInstance.form.getRawValue().title).toBe('');
      expect(el(fixture).querySelector('[data-copy-notice]')?.textContent).toContain('could not be copied');
    });

    it('does nothing special without copyFrom', () => {
      const fixture = create();

      expect(getByIdSpy).not.toHaveBeenCalled();
      expect(el(fixture).querySelector('[data-copy-notice]')).toBeNull();
    });
  });

  describe('assignee from the chosen project\'s team', () => {
    function notice(fixture: ComponentFixture<TaskCreateComponent>): string | null {
      return el(fixture).querySelector('[data-assignee-notice]')?.textContent?.trim() ?? null;
    }

    function options(fixture: ComponentFixture<TaskCreateComponent>): string[] {
      return Array.from(el(fixture).querySelectorAll('#task-assignee option')).map((option) => option.textContent?.trim() ?? '');
    }

    function choose(fixture: ComponentFixture<TaskCreateComponent>, projectId: number | null): void {
      fixture.componentInstance.form.controls.projectId.setValue(projectId);
      fixture.detectChanges();
    }

    it('offers nobody and asks for a project until one is chosen', () => {
      const fixture = create();

      expect(candidatesSpy).not.toHaveBeenCalled();
      expect(options(fixture)).toEqual(['Select a project first…']);
      expect(notice(fixture)).toBeNull();
    });

    it("lists only the project's team, each with their current workload", () => {
      const fixture = create();

      choose(fixture, 5);

      expect(candidatesSpy).toHaveBeenCalledOnceWith(5);
      expect(options(fixture)).toEqual([
        'Select an employee…',
        'Ann Varghese (EMP-004) - 1 active',
        'Joe Thomas (EMP-003) - 3 active - 1 overdue'
      ]);
    });

    it('selects the least-loaded team member and says why', () => {
      const fixture = create();

      choose(fixture, 5);

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(4);
      expect(notice(fixture)).toBe("Auto-selected: fewest active tasks on this project's team (1 active).");
    });

    it('lets the user choose someone else, keeps that choice and hides the auto-selected line', () => {
      const fixture = create();
      choose(fixture, 5);

      fixture.componentInstance.form.controls.assignedToUserId.setValue(3);
      fixture.detectChanges();

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(3);
      expect(notice(fixture)).toBeNull();
      expect(candidatesSpy).toHaveBeenCalledTimes(1);
    });

    it('fetches the team again whenever the project changes and re-picks', () => {
      candidatesSpy.and.callFake((projectId: number) =>
        of(projectId === 5 ? team : [candidate({ userId: 7, fullName: 'Ravi Nair', employeeCode: 'EMP-007', activeTasks: 0 })])
      );
      const fixture = create();

      choose(fixture, 5);
      fixture.componentInstance.form.controls.assignedToUserId.setValue(3);
      choose(fixture, 6);

      expect(candidatesSpy.calls.allArgs()).toEqual([[5], [6]]);
      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(7);
      expect(options(fixture)).toEqual(['Select an employee…', 'Ravi Nair (EMP-007) - 0 active']);
    });

    it('fetches again even when switching back to a project seen before', () => {
      const fixture = create();

      choose(fixture, 5);
      choose(fixture, null);
      choose(fixture, 5);

      expect(candidatesSpy).toHaveBeenCalledTimes(2);
    });

    it('ignores a reply for a project that is no longer chosen', () => {
      const first = new Subject<AssigneeCandidate[]>();
      const second = new Subject<AssigneeCandidate[]>();
      candidatesSpy.and.callFake((projectId: number) => (projectId === 5 ? first : second));
      const fixture = create();

      choose(fixture, 5);
      choose(fixture, 6);
      second.next([candidate({ userId: 7, fullName: 'Ravi Nair', activeTasks: 0 })]);
      first.next(team);
      fixture.detectChanges();

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(7);
      expect(fixture.componentInstance.candidates().map((entry) => entry.userId)).toEqual([7]);
    });

    it('shows a loading placeholder while the team is on its way', () => {
      candidatesSpy.and.returnValue(new Subject<AssigneeCandidate[]>());
      const fixture = create();

      choose(fixture, 5);

      expect(options(fixture)).toEqual(["Loading the project's team…"]);
      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBeNull();
    });

    it('says when nobody is mapped to the project and leaves the assignee empty', () => {
      candidatesSpy.and.returnValue(of([]));
      const fixture = create();

      choose(fixture, 5);

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBeNull();
      expect(notice(fixture)).toBe('No employees are mapped to this project.');
    });

    it('says when the team could not be loaded and leaves the assignee empty', () => {
      candidatesSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
      const fixture = create();

      choose(fixture, 5);

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBeNull();
      expect(notice(fixture)).toBe("This project's team could not be loaded.");
      expect(el(fixture).querySelector('form')).toBeTruthy();
    });

    it('recovers on the next project change after a failed load', () => {
      candidatesSpy.and.returnValues(throwError(() => new HttpErrorResponse({ status: 500 })), of(team));
      const fixture = create();

      choose(fixture, 5);
      choose(fixture, 6);

      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(4);
    });

    it('applies the pick for a project given in the URL', () => {
      const fixture = create({ projectId: '5' });

      expect(candidatesSpy).toHaveBeenCalledWith(5);
      expect(fixture.componentInstance.form.getRawValue().assignedToUserId).toBe(4);
    });

    it('labels a candidate without a code or overdue tasks plainly', () => {
      expect(candidateLabel(candidate({ fullName: 'Lee', employeeCode: null, activeTasks: 0, overdueTasks: 0 }))).toBe('Lee - 0 active');
    });
  });

  it('loads active users for the recipients', () => {
    create();

    expect(getAllSpy).toHaveBeenCalledWith({ isActive: true });
  });

  it('pre-fills the reminder fields from the configured defaults', () => {
    getSettingsSpy.and.returnValue(of(settings({ defaultFirstDays: 3, defaultSecondDays: 0, defaultOverdueFrequency: 'Weekly' })));
    const fixture = create();

    const value = fixture.componentInstance.form.getRawValue();
    expect(getSettingsSpy).toHaveBeenCalled();
    expect(value.firstReminderDays).toBe(3);
    expect(value.secondReminderDays).toBe(0);
    expect(value.overdueReminderFrequency).toBe('Weekly');
    const first = el(fixture).querySelector('#task-first-reminder') as HTMLSelectElement;
    expect(first.selectedOptions[0].textContent?.trim()).toBe('3 days before the deadline');
  });

  it('offers a configured default that is not a predefined option', () => {
    getSettingsSpy.and.returnValue(of(settings({ defaultFirstDays: 7 })));
    const fixture = create();

    const first = el(fixture).querySelector('#task-first-reminder') as HTMLSelectElement;
    expect(first.selectedOptions[0].textContent?.trim()).toBe('7 days before the deadline');
  });

  it('pre-selects the configured default priority while the field is untouched', () => {
    getAppSettingsSpy.and.returnValue(of({ name: 'TaskFlow', timezone: 'UTC', defaultPriority: 'Critical', inAppNotificationsEnabled: true }));
    const fixture = create();

    expect(fixture.componentInstance.form.controls.priority.value).toBe('Critical');
  });

  it('keeps a manually chosen priority over the configured default', () => {
    getAppSettingsSpy.and.returnValue(of({ name: 'TaskFlow', timezone: 'UTC', defaultPriority: 'Critical', inAppNotificationsEnabled: true }));
    const fixture = create();

    fillValid(fixture);
    expect(fixture.componentInstance.form.controls.priority.value).toBe('High');
  });

  it('stays usable when the application settings cannot be loaded', () => {
    getAppSettingsSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    const fixture = create();

    expect(fixture.componentInstance.form.controls.priority.value).toBe('Medium');
    expect(el(fixture).querySelector('form')).toBeTruthy();
  });

  it('stays usable and says so when the reminder defaults cannot be loaded', () => {
    getSettingsSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403 })));
    const fixture = create();

    const value = fixture.componentInstance.form.getRawValue();
    expect(value.firstReminderDays).toBe(2);
    expect(value.secondReminderDays).toBe(1);
    expect(value.overdueReminderFrequency).toBe('Daily');
    expect(el(fixture).querySelector('.tf-alert')).toBeNull();
    expect(el(fixture).querySelector('[data-hint="reminder-defaults"]')).not.toBeNull();
  });

  it('sends untouched reminder fields as null when the defaults were not loaded, so the server applies them', () => {
    getSettingsSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    createSpy.and.returnValue(of({ id: 7, taskCode: 'TSK-000007', assignedToName: 'Joe Thomas' } as TaskDetails));
    const fixture = create();
    fillValid(fixture);

    const overdue = fixture.componentInstance.form.controls.overdueReminderFrequency;
    overdue.setValue('Weekly');
    overdue.markAsDirty();
    submit(fixture);

    const body = createSpy.calls.mostRecent().args[0];
    expect(body.firstReminderDays).toBeNull();
    expect(body.secondReminderDays).toBeNull();
    expect(body.overdueReminderFrequency).toBe('Weekly');
  });

  it('sends the pre-filled reminder values once the defaults have loaded', () => {
    getSettingsSpy.and.returnValue(of(settings({ defaultFirstDays: 3 })));
    createSpy.and.returnValue(of({ id: 8, taskCode: 'TSK-000008', assignedToName: 'Joe Thomas' } as TaskDetails));
    const fixture = create();
    fillValid(fixture);

    submit(fixture);

    const body = createSpy.calls.mostRecent().args[0];
    expect(body.firstReminderDays).toBe(3);
    expect(body.secondReminderDays).toBe(1);
    expect(body.overdueReminderFrequency).toBe('Daily');
    expect(el(fixture).querySelector('[data-hint="reminder-defaults"]')).toBeNull();
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
    expect(error(fixture, 'projectId')).toBe('Project is required.');
    expect(error(fixture, 'assignedToUserId')).toBe('Select the employee to assign.');
    expect(error(fixture, 'startDate')).toBe('Start date is required.');
    expect(error(fixture, 'deadline')).toBe('Deadline is required.');
    expect(error(fixture, 'estimatedHours')).toBe('Estimated hours are required.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('does not submit without a project', () => {
    const fixture = create();
    fillValid(fixture);
    fixture.componentInstance.form.patchValue({ projectId: null });

    submit(fixture);

    expect(error(fixture, 'projectId')).toBe('Project is required.');
    expect(createSpy).not.toHaveBeenCalled();
  });

  it('lists the projects to choose from', () => {
    const fixture = create();

    const options = Array.from(el(fixture).querySelectorAll('#task-project option')).map((option) => option.textContent?.trim());
    expect(options).toEqual(['Select a project…', 'Apollo']);
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
      projectId: 5,
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
