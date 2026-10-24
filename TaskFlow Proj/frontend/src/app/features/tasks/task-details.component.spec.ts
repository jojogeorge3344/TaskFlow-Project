import { HttpErrorResponse } from '@angular/common/http';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { TaskDetails } from '../../core/models';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskDetailsComponent } from './task-details.component';
import { TaskService } from './task.service';

describe('TaskDetailsComponent', () => {
  const details: TaskDetails = {
    id: 7,
    taskCode: 'TSK-1007',
    title: 'Integrate payments',
    description: 'Wire up the payment gateway.',
    assignedToUserId: 3,
    assignedToName: 'Joe',
    createdByUserId: 1,
    createdByName: 'Boss',
    priority: 'High',
    startDate: '2026-10-01T08:00:00Z',
    deadline: '2026-10-20T10:00:00Z',
    estimatedHours: 8,
    status: 'ToDo',
    stage: 'Upcoming',
    recipients: [{ userId: 9, fullName: 'Sara' }],
    createdAt: '2026-10-01T08:00:00Z',
    updatedAt: '2026-10-01T08:00:00Z',
    projectId: 4,
    projectName: 'Apollo',
    firstReminderDays: 2,
    secondReminderDays: 1,
    overdueReminderFrequency: 'Daily'
  };

  let fixture: ComponentFixture<TaskDetailsComponent>;
  let component: TaskDetailsComponent;
  let getByIdSpy: jasmine.Spy;
  let changeStatusSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };

  function create(role: string, task: TaskDetails = details, returnUrl: string | null = null): void {
    getByIdSpy = jasmine.createSpy('getById').and.returnValue(of(task));
    changeStatusSpy = jasmine.createSpy('changeStatus');
    updateSpy = jasmine.createSpy('update');
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));
    toast = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      imports: [TaskDetailsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => '7' }, queryParamMap: { get: (key: string) => (key === 'returnUrl' ? returnUrl : null) } } }
        },
        {
          provide: AuthService,
          useValue: fakeAuthService(() => ({ id: 1, fullName: 'Boss', role, permissions: systemPermissions(role) }))
        },
        {
          provide: TaskService,
          useValue: { getById: getByIdSpy, changeStatus: changeStatusSpy, update: updateSpy, delete: deleteSpy }
        },
        { provide: ToastService, useValue: toast }
      ]
    });

    fixture = TestBed.createComponent(TaskDetailsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  describe('Back button', () => {
    function backButton(): HTMLButtonElement {
      return fixture.nativeElement.querySelector('[data-action="back"]');
    }

    it('returns to the project the user came from', () => {
      create('Manager', details, '/projects/4');
      const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl');

      expect(backButton().textContent).toContain('Back to project');
      backButton().click();

      expect(navigate).toHaveBeenCalledWith('/projects/4');
    });

    it('returns to the task list and the board when they are the origin', () => {
      create('Manager', details, '/tasks');
      expect(backButton().textContent).toContain('Back to tasks');

      TestBed.resetTestingModule();
      create('Manager', details, '/board');
      expect(backButton().textContent).toContain('Back to board');
    });

    it('falls back to the task list when opened directly or with an external location', () => {
      for (const returnUrl of [null, '//evil.com', 'https://evil.com']) {
        TestBed.resetTestingModule();
        create('Manager', details, returnUrl);
        const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl');

        backButton().click();

        expect(navigate).withContext(String(returnUrl)).toHaveBeenCalledWith('/tasks');
      }
    });
  });

  describe('project', () => {
    it('shows the project name in the header and details', () => {
      create('Manager');

      expect(fixture.nativeElement.querySelector('[data-project]').textContent).toContain('Apollo');
      expect(fixture.nativeElement.querySelector('[data-meta-project]').textContent).toContain('Apollo');
    });

    it('shows a dash for a legacy task without a project', () => {
      create('Manager', { ...details, projectId: null, projectName: null });

      expect(fixture.nativeElement.querySelector('[data-meta-project]').textContent.trim()).toBe('—');
      expect(fixture.nativeElement.querySelector('[data-action="edit-task"]')).not.toBeNull();
    });

    it('shows the project read-only while editing and does not send it on save', () => {
      create('Manager');
      updateSpy.and.returnValue(of(details));

      (fixture.nativeElement.querySelector('[data-action="edit-task"]') as HTMLButtonElement).click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('[data-edit-project]').textContent).toContain('Apollo');
      expect(fixture.nativeElement.querySelector('[data-edit-form] select[formcontrolname="projectId"]')).toBeNull();
      component.saveEdit();
      expect(Object.keys(updateSpy.calls.mostRecent().args[1])).not.toContain('projectId');
    });
  });

  describe('copy', () => {
    it('links to the pre-filled create form for someone who can create tasks', () => {
      create('Manager');

      const link = fixture.nativeElement.querySelector('[data-action="copy-task"]') as HTMLAnchorElement;
      expect(link.getAttribute('href')).toBe('/tasks/new?copyFrom=7');
    });

    it('is hidden from an employee', () => {
      create('Employee');

      expect(fixture.nativeElement.querySelector('[data-action="copy-task"]')).toBeNull();
    });
  });

  describe('edit form layout', () => {
    it('groups the fields into Details and Schedule sections with an action bar', () => {
      create('Manager');
      (fixture.nativeElement.querySelector('[data-action="edit-task"]') as HTMLButtonElement).click();
      fixture.detectChanges();

      const form = fixture.nativeElement.querySelector('[data-edit-form]') as HTMLElement;
      const sections = Array.from(form.querySelectorAll('.tf-section__title')).map((h) => h.textContent?.trim());
      expect(sections).toEqual(['Details', 'Schedule']);
      expect(form.querySelector('[data-edit-actions]')).not.toBeNull();
      for (const field of ['title', 'description', 'priority', 'startDate', 'deadline', 'estimatedHours']) {
        expect(form.querySelector(`[data-field="${field}"]`)).withContext(field).not.toBeNull();
      }
    });

    it('puts the deadline error next to the deadline field and blocks saving', () => {
      create('Manager');
      (fixture.nativeElement.querySelector('[data-action="edit-task"]') as HTMLButtonElement).click();
      component.editForm.patchValue({ startDate: '2026-10-10T10:00', deadline: '2026-10-09T10:00' });
      fixture.detectChanges();

      const error = fixture.nativeElement.querySelector('[data-error="deadline"]') as HTMLElement;
      expect(error.closest('.tf-field')?.querySelector('#edit-deadline')).not.toBeNull();
      expect((fixture.nativeElement.querySelector('[data-edit-form] button[type="submit"]') as HTMLButtonElement).disabled).toBeTrue();
    });
  });

  it('renders the task title, stage badge and recipient chips', () => {
    create('Manager');

    expect(fixture.nativeElement.textContent).toContain('Integrate payments');
    expect(fixture.nativeElement.textContent).toContain('TSK-1007');
    expect(fixture.nativeElement.textContent).toContain('Upcoming');
    expect(fixture.nativeElement.textContent).toContain('Sara');
  });

  it('exposes the overdue stage and hides the edit form for employees', () => {
    create('Employee', { ...details, stage: 'Overdue' });

    expect(component.stageLabel(component.task()!.stage)).toBe('Overdue');
    expect(component.canEdit()).toBeFalse();
    expect(component.canReassign()).toBeFalse();
  });

  it('calls changeStatus for an allowed transition and reloads details', () => {
    create('Employee');
    changeStatusSpy.and.returnValue(of({}));

    component.changeStatus('InProgress');

    expect(changeStatusSpy).toHaveBeenCalledWith(7, { status: 'InProgress' });
    expect(getByIdSpy).toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalled();
  });

  it('does not offer status actions for terminal stages', () => {
    create('Employee', { ...details, status: 'Completed' });

    expect(component.allowedNext('Completed')).toEqual([]);
    expect(fixture.nativeElement.textContent).not.toContain('Move to');
  });

  it('offers In-QA from In Progress and allows send-back or completion from In-QA', () => {
    create('Employee', { ...details, status: 'InProgress' });

    expect(component.allowedNext('InProgress')).toContain('InQA');
    expect(component.allowedNext('InQA')).toEqual(['InProgress', 'Blocked', 'Completed', 'Cancelled']);
    expect(fixture.nativeElement.querySelector('[data-action="status-InQA"]')).not.toBeNull();
  });

  it('does not offer a direct jump from To Do to In-QA', () => {
    create('Employee', { ...details, status: 'ToDo' });

    expect(component.allowedNext('ToDo')).not.toContain('InQA');
  });

  it('blocks edit submissions while the form is invalid', () => {
    create('Manager');

    component.startEdit();
    component.editForm.controls.title.setValue('');
    component.editForm.markAllAsTouched();
    component.saveEdit();

    expect(updateSpy).not.toHaveBeenCalled();
  });

  it('blocks edit submissions when the deadline precedes the start date', () => {
    create('Manager');

    component.startEdit();
    component.editForm.patchValue({
      startDate: '2026-10-20T08:00',
      deadline: '2026-10-10T08:00'
    });
    component.saveEdit();

    expect(component.editForm.errors?.['deadlineBeforeStart']).toBeTrue();
    expect(updateSpy).not.toHaveBeenCalled();
  });

  it('submits a valid edit and updates the task', () => {
    create('Manager');
    updateSpy.and.returnValue(of({ ...details, title: 'Renamed task' }));

    component.startEdit();
    component.editForm.controls.title.setValue('Renamed task');
    component.saveEdit();

    expect(updateSpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ title: 'Renamed task' }));
    expect(component.task()?.title).toBe('Renamed task');
    expect(component.editing()).toBeFalse();
    expect(toast.success).toHaveBeenCalled();
  });

  it('toasts a failure when changing status returns a 4xx', () => {
    create('Employee');
    changeStatusSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, statusText: 'Bad Request', error: { detail: 'Invalid transition' } }))
    );

    component.changeStatus('Completed');

    expect(component.changingStatus()).toBeNull();
    expect(toast.error).toHaveBeenCalledWith('Invalid transition');
  });

  describe('delete', () => {
    afterEach(() => document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close()));

    function deleteButton(): HTMLButtonElement | null {
      return (fixture.nativeElement as HTMLElement).querySelector('[data-action="delete-task"]');
    }

    it('is not offered to a Manager who can edit and reassign', () => {
      create('Manager');

      expect(component.canEdit()).toBeTrue();
      expect(deleteButton()).toBeNull();
    });

    it('is not offered to an Employee', () => {
      create('Employee');

      expect(deleteButton()).toBeNull();
    });

    it('is offered to an Administrator', () => {
      create('Administrator');

      expect(deleteButton()).not.toBeNull();
    });

    it('does nothing when the confirmation is cancelled', fakeAsync(() => {
      create('Administrator');

      deleteButton()!.click();
      fixture.detectChanges();
      const dialog = (fixture.nativeElement as HTMLElement).querySelector('dialog') as HTMLDialogElement;
      expect(dialog.open).toBeTrue();
      expect(dialog.textContent).toContain('TSK-1007');
      (dialog.querySelector('[data-action="cancel"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(deleteSpy).not.toHaveBeenCalled();
    }));

    it('deletes after confirmation, shows a toast and returns to the task list', fakeAsync(() => {
      create('Administrator');
      const navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

      deleteButton()!.click();
      fixture.detectChanges();
      ((fixture.nativeElement as HTMLElement).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(deleteSpy).toHaveBeenCalledWith(7);
      expect(toast.success).toHaveBeenCalledWith('TSK-1007 deleted');
      expect(navigateSpy).toHaveBeenCalledWith(['/tasks']);
    }));

    it('stays on the page and shows the server message when the delete is rejected', fakeAsync(() => {
      create('Administrator');
      const navigateSpy = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
      deleteSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404, error: { error: 'Task not found.' } })));

      deleteButton()!.click();
      fixture.detectChanges();
      ((fixture.nativeElement as HTMLElement).querySelector('dialog [data-action="confirm"]') as HTMLButtonElement).click();
      flushMicrotasks();

      expect(toast.error).toHaveBeenCalledWith('Task not found.');
      expect(navigateSpy).not.toHaveBeenCalled();
    }));
  });

  it('shows the error state when details cannot be loaded', () => {
    create('Employee');
    getByIdSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })));

    component.load(7);

    expect(component.error()).not.toBeNull();
    expect(component.loading()).toBeFalse();
  });
});