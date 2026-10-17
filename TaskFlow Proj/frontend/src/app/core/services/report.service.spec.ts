import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReportSummary } from '../models';
import { ReportService } from './report.service';

describe('ReportService', () => {
  let service: ReportService;
  let http: HttpTestingController;

  const summary: ReportSummary = { totalTasks: 10, pendingTasks: 4, completedTasks: 5, overdueTasks: 1 };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReportService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the summary with the supplied filters', () => {
    let received: ReportSummary | undefined;
    service
      .getSummary({
        deadlineFrom: '2026-10-01T00:00:00.000Z',
        deadlineTo: '2026-10-31T23:59:59.999Z',
        assignedToUserId: 3,
        departmentId: 7,
        status: 'Completed',
        priority: 'High'
      })
      .subscribe((result) => (received = result));

    const request = http.expectOne((req) => req.url === '/api/v1/reports');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('deadlineFrom')).toBe('2026-10-01T00:00:00.000Z');
    expect(request.request.params.get('deadlineTo')).toBe('2026-10-31T23:59:59.999Z');
    expect(request.request.params.get('assignedToUserId')).toBe('3');
    expect(request.request.params.get('departmentId')).toBe('7');
    expect(request.request.params.get('status')).toBe('Completed');
    expect(request.request.params.get('priority')).toBe('High');
    request.flush(summary);

    expect(received).toEqual(summary);
  });

  it('omits empty filters from the summary request', () => {
    service.getSummary({ assignedToUserId: null, status: null }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/v1/reports');
    expect(request.request.params.keys()).toEqual([]);
    request.flush(summary);
  });

  it('downloads the CSV export as a blob with the same filters', () => {
    service.exportCsv({ priority: 'Critical', assignedToUserId: 4 }).subscribe();

    const request = http.expectOne((req) => req.url === '/api/v1/reports/export');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('priority')).toBe('Critical');
    expect(request.request.params.get('assignedToUserId')).toBe('4');
    request.flush(new Blob(['TaskCode\n'], { type: 'text/csv' }));
  });
});
