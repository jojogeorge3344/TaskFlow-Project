import { ComponentFixture, TestBed } from '@angular/core/testing';
import { systemPermissions } from '../../testing/permissions';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { EmployeeDashboardStats } from '../../core/models';
import { DashboardService } from '../../core/services/dashboard.service';
import { EmployeeDashboardComponent } from './employee-dashboard.component';

describe('EmployeeDashboardComponent', () => {
  const userSignal = signal<AuthUser | null>({
    id: 3,
    fullName: 'Employee User',
    username: 'employee',
    email: 'employee@taskflow.com',
    role: 'Employee',
    employeeCode: null,
    permissions: systemPermissions('Employee')
  });

  const stats: EmployeeDashboardStats = {
    assignedTasks: 6,
    pendingTasks: 3,
    tasksDueToday: 1,
    overdueTasks: 1,
    inProgressTasks: 2,
    completedTasks: 3
  };

  let getEmployeeStatsSpy: jasmine.Spy;

  function createComponent(): ComponentFixture<EmployeeDashboardComponent> {
    const fixture = TestBed.createComponent(EmployeeDashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    getEmployeeStatsSpy = jasmine.createSpy('getEmployeeStats');

    await TestBed.configureTestingModule({
      imports: [EmployeeDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardService, useValue: { getEmployeeStats: getEmployeeStatsSpy } },
        { provide: AuthService, useValue: { currentUser: userSignal.asReadonly(), logout: () => of(undefined) } }
      ]
    }).compileComponents();
  });

  it('renders personal statistics, progress and the task list empty state', () => {
    getEmployeeStatsSpy.and.returnValue(of(stats));
    const element = createComponent().nativeElement as HTMLElement;

    const values = Array.from(element.querySelectorAll('.stat-card__value')).map((el) => el.textContent?.trim());

    expect(values).toEqual(['6', '3', '1', '1', '2', '3']);
    expect(element.querySelector('.dashboard__welcome')?.textContent).toContain('Employee User');

    const bar = element.querySelector('.progress__bar') as HTMLElement;
    expect(bar.style.width).toBe('50%');

    const emptyState = element.querySelector('.empty-state');
    expect(emptyState?.textContent).toContain('task list API is not implemented');
  });

  it('does not divide by zero when the employee has no tasks', () => {
    getEmployeeStatsSpy.and.returnValue(
      of<EmployeeDashboardStats>({
        assignedTasks: 0,
        pendingTasks: 0,
        tasksDueToday: 0,
        overdueTasks: 0,
        inProgressTasks: 0,
        completedTasks: 0
      })
    );

    const fixture = createComponent();

    expect(fixture.componentInstance.completionRate()).toBe(0);
  });

  it('shows an error state and retries', () => {
    getEmployeeStatsSpy.and.returnValue(throwError(() => new Error('boom')));
    const fixture = createComponent();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.dashboard__error')).toBeTruthy();

    getEmployeeStatsSpy.and.returnValue(of(stats));
    fixture.componentInstance.load();
    fixture.detectChanges();

    expect(element.querySelector('.dashboard__error')).toBeFalsy();
    expect(element.querySelectorAll('.stat-card').length).toBe(6);
  });
});
