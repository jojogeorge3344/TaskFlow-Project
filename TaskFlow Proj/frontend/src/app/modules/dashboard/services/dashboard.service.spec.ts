import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EmployeeDashboardStats } from '../models/dashboard.model';
import { DashboardService } from './dashboard.service';

describe('DashboardService', () => {
  let service: DashboardService;
  let httpMock: HttpTestingController;

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
    service.getEmployeeStats().subscribe({ error: () => (failed = true) });

    httpMock.expectOne('/api/v1/dashboard/employee').flush({}, { status: 500, statusText: 'Server Error' });

    expect(failed).toBeTrue();
  });
});
