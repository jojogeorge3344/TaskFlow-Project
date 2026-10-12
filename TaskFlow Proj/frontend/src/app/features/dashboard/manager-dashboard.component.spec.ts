import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { AuthUser } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { ManagerDashboardStats } from '../../core/models';
import { DashboardService } from '../../core/services/dashboard.service';
import { ManagerDashboardComponent } from './manager-dashboard.component';

describe('ManagerDashboardComponent', () => {
  const userSignal = signal<AuthUser | null>({
    id: 2,
    fullName: 'Manager User',
    username: 'manager',
    email: 'manager@taskflow.local',
    role: 'Manager',
    employeeCode: null
  });

  const stats: ManagerDashboardStats = {
    totalTasks: 9,
    pendingTasks: 5,
    tasksDueToday: 2,
    upcomingDeadlines: 3,
    overdueTasks: 1,
    completedTasks: 3
  };

  let getManagerStatsSpy: jasmine.Spy;

  function createComponent(): ComponentFixture<ManagerDashboardComponent> {
    const fixture = TestBed.createComponent(ManagerDashboardComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    getManagerStatsSpy = jasmine.createSpy('getManagerStats');

    await TestBed.configureTestingModule({
      imports: [ManagerDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: DashboardService, useValue: { getManagerStats: getManagerStatsSpy } },
        { provide: AuthService, useValue: { currentUser: userSignal.asReadonly(), logout: () => of(undefined) } }
      ]
    }).compileComponents();
  });

  it('renders team statistics and the progress summary', () => {
    getManagerStatsSpy.and.returnValue(of(stats));
    const element = createComponent().nativeElement as HTMLElement;

    const values = Array.from(element.querySelectorAll('.stat-card__value')).map((el) => el.textContent?.trim());

    expect(values).toEqual(['9', '5', '2', '3', '1', '3']);
    expect(element.querySelector('.dashboard__welcome')?.textContent).toContain('Manager User');

    const bar = element.querySelector('.progress__bar') as HTMLElement;
    expect(bar.style.width).toBe('33%');
  });

  it('shows an error state and retries', () => {
    getManagerStatsSpy.and.returnValue(throwError(() => new Error('boom')));
    const fixture = createComponent();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.dashboard__error')).toBeTruthy();

    getManagerStatsSpy.and.returnValue(of(stats));
    fixture.componentInstance.load();
    fixture.detectChanges();

    expect(element.querySelector('.dashboard__error')).toBeFalsy();
    expect(fixture.componentInstance.completionRate()).toBe(33);
  });
});
