import { ComponentFixture, TestBed } from '@angular/core/testing';
import { systemPermissions } from '../../testing/permissions';
import { provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { AdminDashboardStats } from '../../core/models';
import { DashboardService } from '../../core/services/dashboard.service';
import { AdminDashboardComponent } from './admin-dashboard.component';

describe('AdminDashboardComponent', () => {
  const userSignal = signal<AuthUser | null>({
    id: 1,
    fullName: 'Admin User',
    username: 'admin',
    email: 'admin@taskflow.local',
    role: 'Administrator',
    employeeCode: null,
    permissions: systemPermissions('Administrator')
  });

  const stats: AdminDashboardStats = {
    totalEmployees: 4,
    activeEmployees: 3,
    totalDepartments: 2,
    totalTasks: 9,
    pendingTasks: 5,
    completedTasks: 3,
    overdueTasks: 1
  };

  let getAdminStatsSpy: jasmine.Spy;

  function createComponent(): ComponentFixture<AdminDashboardComponent> {
    const fixture = TestBed.createComponent(AdminDashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    getAdminStatsSpy = jasmine.createSpy('getAdminStats');

    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardService, useValue: { getAdminStats: getAdminStatsSpy } },
        { provide: AuthService, useValue: { currentUser: userSignal.asReadonly(), logout: () => of(undefined) } }
      ]
    }).compileComponents();
  });

  it('renders administrator statistics and the welcome message', () => {
    getAdminStatsSpy.and.returnValue(of(stats));
    const element = createComponent().nativeElement as HTMLElement;

    const values = Array.from(element.querySelectorAll('.stat-card__value')).map((el) => el.textContent?.trim());

    expect(values).toEqual(['4', '3', '2', '9', '5', '3', '1']);
    expect(element.querySelector('.dashboard__welcome')?.textContent).toContain('Admin User');
    expect(element.querySelector('.empty-state')).toBeFalsy();
  });

  it('shows a loading state while the request is pending', () => {
    getAdminStatsSpy.and.returnValue(new Subject<AdminDashboardStats>());
    const element = createComponent().nativeElement as HTMLElement;

    expect(element.querySelector('.dashboard__status')).toBeTruthy();
  });

  it('shows an error state that can be retried', () => {
    getAdminStatsSpy.and.returnValue(throwError(() => new Error('boom')));
    const fixture = createComponent();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.dashboard__error')).toBeTruthy();

    getAdminStatsSpy.and.returnValue(of(stats));
    fixture.componentInstance.load();
    fixture.detectChanges();

    expect(element.querySelector('.dashboard__error')).toBeFalsy();
    expect(element.querySelectorAll('.stat-card').length).toBe(7);
  });
});
