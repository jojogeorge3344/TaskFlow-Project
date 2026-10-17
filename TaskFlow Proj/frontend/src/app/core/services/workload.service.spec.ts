import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EmployeeWorkload } from '../models';
import { WorkloadService } from './workload.service';

describe('WorkloadService', () => {
  let service: WorkloadService;
  let http: HttpTestingController;

  const rows: EmployeeWorkload[] = [
    {
      userId: 3,
      fullName: 'Joe Thomas',
      departmentId: 2,
      departmentName: 'Engineering',
      activeTasks: 4,
      highPriorityTasks: 2,
      overdueTasks: 1,
      completedTasks: 5
    }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(WorkloadService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the workload without a department filter', () => {
    let received: EmployeeWorkload[] | undefined;
    service.getWorkload().subscribe((rows) => (received = rows));

    const request = http.expectOne((req) => req.url === '/api/v1/workload');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.has('departmentId')).toBeFalse();
    request.flush([{ userId: 3, fullName: 'Joe Thomas', departmentId: null, departmentName: null, activeTasks: 2, highPriorityTasks: 1, overdueTasks: 0, completedTasks: 4 }]);

    expect(received?.length).toBe(1);
  });

  it('narrows the workload to a department', () => {
    service.getWorkload(7).subscribe();

    const request = http.expectOne((req) => req.url === '/api/v1/workload');
    expect(request.request.params.get('departmentId')).toBe('7');
    request.flush([]);
  });

  it('propagates API errors', () => {
    let failed = false;
    service.getWorkload().subscribe({ error: () => (failed = true) });

    http.expectOne('/api/v1/workload').flush({}, { status: 403, statusText: 'Forbidden' });

    expect(failed).toBeTrue();
  });
});
