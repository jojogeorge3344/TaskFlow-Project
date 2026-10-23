import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { User } from '../../core/models';
import { ProfileService } from '../../core/services/profile.service';
import { ToastService } from '../../shared/ui/toast.service';
import { ProfileComponent } from './profile.component';
import { NO_ALLOCATIONS, withProject } from '../../testing/users';

describe('ProfileComponent', () => {
  const me: User = {
    id: 3,
    employeeCode: 'EMP-3',
    fullName: 'Jo Jo',
    username: 'jojo',
    email: 'jojo@taskflow.com',
    role: 'Employee',
    roleId: 3,
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    ...withProject(1, 'Engineering'),
    departments: [],
    managers: []
  };

  let getSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let toast: jasmine.SpyObj<ToastService>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    getSpy = jasmine.createSpy('get').and.returnValue(of(me));
    updateSpy = jasmine.createSpy('update');
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['clearSession', 'applyProfile']);
  });

  function create(): ComponentFixture<ProfileComponent> {
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: { get: getSpy, update: updateSpy } },
        { provide: AuthService, useValue: authService },
        { provide: ToastService, useValue: toast },
        provideRouter([])
      ]
    });
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ProfileComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function input(fixture: ComponentFixture<ProfileComponent>, field: string): HTMLInputElement {
    return el(fixture).querySelector(`[data-field="${field}"]`) as HTMLInputElement;
  }

  function bullets(fixture: ComponentFixture<ProfileComponent>, label: string): string[] {
    return Array.from(el(fixture).querySelectorAll(`[data-field="readonly-${label}"] li`)).map((item) => item.textContent!.trim());
  }

  function submit(fixture: ComponentFixture<ProfileComponent>, action: string): void {
    (el(fixture).querySelector(`[data-action="${action}"]`) as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  it('shows the own profile with read-only account fields', () => {
    const fixture = create();

    expect(getSpy).toHaveBeenCalled();
    expect(input(fixture, 'fullName').value).toBe('Jo Jo');
    expect(input(fixture, 'email').value).toBe('jojo');
    expect(el(fixture).querySelector('[data-email-suffix]')?.textContent?.trim()).toBe('@taskflow.com');
    expect(el(fixture).querySelector('[data-field="readonly-Username"]')?.textContent?.trim()).toBe('jojo');
    expect(el(fixture).querySelector('[data-field="readonly-Role"]')?.textContent?.trim()).toBe('Employee');
    const projects = el(fixture).querySelector('[data-field="readonly-Projects"]')!;
    expect(projects.querySelector('ul')).toBeNull();
    expect(projects.textContent).toContain('Engineering 100% (Member)');
    expect(projects.querySelector('.profile-readonly__note')?.textContent?.trim()).toBe('100% allocated');
    expect(el(fixture).querySelector('[data-field="readonly-Account status"]')?.textContent?.trim()).toBe('Active');
  });

  it('shows the employee their projects with percentages, their departments and their managers', () => {
    getSpy.and.returnValue(
      of({
        ...me,
        projectAllocations: [
          { projectId: 1, projectName: 'Apollo', positionId: 3, positionName: 'QA', percent: 60 },
          { projectId: 2, projectName: 'Zephyr', positionId: 4, positionName: 'Developer', percent: 30 }
        ],
        totalAllocatedPercent: 90,
        remainingPercent: 10,
        departments: [{ id: 5, name: 'Finance' }, { id: 6, name: 'Human Resources' }],
        managers: [{ id: 20, fullName: 'Anupama Nair' }, { id: 21, fullName: 'Ravi Rao' }]
      })
    );
    const fixture = create();

    expect(bullets(fixture, 'Projects')).toEqual(['Apollo 60% (QA)', 'Zephyr 30% (Developer)']);
    expect(el(fixture).querySelector('[data-field="readonly-Projects"] .profile-readonly__note')?.textContent?.trim()).toBe('90% allocated');
    expect(bullets(fixture, 'Departments')).toEqual(['Finance', 'Human Resources']);
    expect(bullets(fixture, 'Managers')).toEqual(['Anupama Nair', 'Ravi Rao']);
    expect(el(fixture).querySelector('[data-field="readonly-Team"]')).toBeNull();
  });

  it('never shows several values as one comma-separated line', () => {
    getSpy.and.returnValue(
      of({ ...me, departments: [{ id: 5, name: 'Finance' }, { id: 6, name: 'Human Resources' }, { id: 7, name: 'Legal' }] })
    );
    const fixture = create();

    expect(bullets(fixture, 'Departments').length).toBe(3);
    expect(el(fixture).querySelector('[data-field="readonly-Departments"]')?.textContent).not.toContain(',');
  });

  it('shows a single value as plain text and no value as a dash', () => {
    getSpy.and.returnValue(of({ ...me, departments: [{ id: 5, name: 'Finance' }], managers: [] }));
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="readonly-Departments"] ul')).toBeNull();
    expect(el(fixture).querySelector('[data-field="readonly-Departments"]')?.textContent?.trim()).toBe('Finance');
    expect(el(fixture).querySelector('[data-field="readonly-Managers"]')?.textContent?.trim()).toBe('—');
  });

  it('lists a manager\'s several managed projects and team members as bullets', () => {
    getSpy.and.returnValue(
      of({
        ...me,
        role: 'Manager',
        roleId: 2,
        managerProjects: [{ id: 1, name: 'Engineering' }, { id: 2, name: 'Atlas' }],
        reports: [{ id: 9, fullName: 'Sara' }, { id: 10, fullName: 'Tom' }, { id: 11, fullName: 'Uma' }]
      })
    );
    const fixture = create();

    expect(bullets(fixture, 'Managed projects')).toEqual(['Engineering', 'Atlas']);
    expect(bullets(fixture, 'Team')).toEqual(['Sara', 'Tom', 'Uma']);
  });

  it('shows a manager their projects and team', () => {
    getSpy.and.returnValue(
      of({
        ...me,
        role: 'Manager',
        roleId: 2,
        managerProjects: [{ id: 1, name: 'Engineering' }],
        reports: [{ id: 9, fullName: 'Sara' }]
      })
    );
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="readonly-Managers"]')?.textContent?.trim()).toBe('—');
    expect(el(fixture).querySelector('[data-field="readonly-Managed projects"]')?.textContent?.trim()).toBe('Engineering');
    expect(el(fixture).querySelector('[data-field="readonly-Team"]')?.textContent?.trim()).toBe('Sara');
  });

  it('saves the self-edit and refreshes the cached session user', () => {
    const updated: User = { ...me, fullName: 'Joanna Jo', email: 'joanna@taskflow.com' };
    updateSpy.and.returnValue(of(updated));
    const fixture = create();

    fixture.componentInstance.form.patchValue({ fullName: 'Joanna Jo', email: 'joanna' });
    submit(fixture, 'save');

    expect(updateSpy).toHaveBeenCalledOnceWith({ fullName: 'Joanna Jo', email: 'joanna@taskflow.com', mobileNumber: '' });
    expect(authService.applyProfile).toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledWith('Profile updated');
    expect(el(fixture).querySelector('[data-status="saved"]')).not.toBeNull();
  });

  it('shows a rejected duplicate email from the server', () => {
    updateSpy.and.returnValue(
      throwError(
        () => new HttpErrorResponse({ status: 400, error: { error: 'Email already exists.' } })
      )
    );
    const fixture = create();

    submit(fixture, 'save');

    expect(el(fixture).querySelector('.tf-alert')?.textContent).toContain('Email already exists.');
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('blocks a malformed email client-side', () => {
    const fixture = create();

    fixture.componentInstance.form.controls.email.setValue('bad name!');
    fixture.detectChanges();
    submit(fixture, 'save');

    expect(updateSpy).not.toHaveBeenCalled();
    expect(el(fixture).querySelector('[data-error="email"]')?.textContent).toContain('before @taskflow.com');
  });

  it('keeps only the part before the @ when a full address is typed or pasted', () => {
    const updated: User = { ...me, email: 'joanna@taskflow.com' };
    updateSpy.and.returnValue(of(updated));
    const fixture = create();
    const field = input(fixture, 'email');

    field.value = 'Joanna@other.com';
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(field.value).toBe('joanna');
    expect(el(fixture).querySelector('[data-email-suffix]')?.textContent?.trim()).toBe('@taskflow.com');

    submit(fixture, 'save');
    expect(updateSpy).toHaveBeenCalledOnceWith(jasmine.objectContaining({ email: 'joanna@taskflow.com' }));
  });

  it('shows a retryable error when loading fails', () => {
    getSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load your profile.');

    getSpy.and.returnValue(of(me));
    (el(fixture).querySelector('[data-action="retry"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(input(fixture, 'fullName').value).toBe('Jo Jo');
  });
});
