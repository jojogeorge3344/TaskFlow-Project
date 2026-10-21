import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdminDashboardStats, EmployeeDashboardStats, ManagerDashboardStats } from '../models';
import { DashboardService } from './dashboard.service';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;

  const adminStats: AdminDashboardStats = {
    totalEmployees: 4,
    activeEmployees: 3,
    totalProjects: 2,
    totalTasks: 9,
    pendingTasks: 5,
    completedTasks: 3,
    overdueTasks: 1
  };

  const managerStats: ManagerDashboardStats = {
    totalTasks: 9,
    pendingTasks: 5,
    tasksDueToday: 2,
    upcomingDeadlines: 3,
    overdueTasks: 1,
    completedTasks: 3
  };

  const employeeStats: EmployeeDashboardStats = {
    assignedTasks: 6,
    pendingTasks: 3,
    tasksDueToday: 1,
    overdueTasks: 1,
    inProgressTasks: 2,
    completedTasks: 3
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(DashboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads admin stats from the admin endpoint', () => {
    let received: AdminDashboardStats | undefined;
    service.getAdminStats().subscribe((stats) => (received = stats));

    const request = httpMock.expectOne('/api/v1/dashboard/admin');
    expect(request.request.method).toBe('GET');
    request.flush(adminStats);

    expect(received).toEqual(adminStats);
  });

  it('loads manager stats from the manager endpoint', () => {
    let received: ManagerDashboardStats | undefined;
    service.getManagerStats().subscribe((stats) => (received = stats));

    const request = httpMock.expectOne('/api/v1/dashboard/manager');
    expect(request.request.method).toBe('GET');
    request.flush(managerStats);

    expect(received).toEqual(managerStats);
  });

  it('loads employee stats from the employee endpoint', () => {
    let received: EmployeeDashboardStats | undefined;
    service.getEmployeeStats().subscribe((stats) => (received = stats));

    const request = httpMock.expectOne('/api/v1/dashboard/employee');
    expect(request.request.method).toBe('GET');
    request.flush(employeeStats);

    expect(received).toEqual(employeeStats);
  });

  it('propagates API errors', () => {
    let failed = false;
    service.getAdminStats().subscribe({ error: () => (failed = true) });

    httpMock.expectOne('/api/v1/dashboard/admin').flush({}, { status: 500, statusText: 'Server Error' });

    expect(failed).toBeTrue();
  });
});
