import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DepartmentWithCount, ReportSummary, TaskRecipient } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { ReportService } from '../../core/services/report.service';
import { endOfLocalDay, startOfLocalDay } from '../../shared/ui/local-day';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskService } from '../tasks/task.service';
import { ReportComponent } from './report.component';

describe('ReportComponent', () => {
  const summary: ReportSummary = { totalTasks: 10, pendingTasks: 4, completedTasks: 5, overdueTasks: 1 };

  const departments: DepartmentWithCount[] = [
    { id: 1, name: 'Engineering', description: null, createdAt: '2026-01-01T00:00:00Z', employeeCount: 2 }
  ];
  const assignees: TaskRecipient[] = [{ userId: 3, fullName: 'Joe Thomas' }];

  let getSummarySpy: jasmine.Spy;
  let exportSpy: jasmine.Spy;
  let toastErrorSpy: jasmine.Spy;

  function create(): ComponentFixture<ReportComponent> {
    const fixture = TestBed.createComponent(ReportComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ReportComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function lastFilter(fixture: ComponentFixture<ReportComponent>): Record<string, unknown> {
    return getSummarySpy.calls.mostRecent().args[0] as Record<string, unknown>;
  }

  beforeEach(() => {
    getSummarySpy = jasmine.createSpy('getSummary').and.returnValue(of(summary));
    exportSpy = jasmine.createSpy('exportCsv').and.returnValue(of(new Blob(['a,b'], { type: 'text/csv' })));
    toastErrorSpy = jasmine.createSpy('error');

    TestBed.configureTestingModule({
      providers: [
        { provide: ReportService, useValue: { getSummary: getSummarySpy, exportCsv: exportSpy } },
        { provide: DepartmentService, useValue: { getAll: () => of(departments) } },
        { provide: TaskService, useValue: { getAssignees: () => of(assignees) } },
        { provide: ToastService, useValue: { error: toastErrorSpy } }
      ]
    });
  });

  it('renders the headline totals and the completion summary', () => {
    const fixture = create();

    const values = Array.from(el(fixture).querySelectorAll('.stat-card__value')).map((node) => node.textContent?.trim());
    expect(values).toEqual(['10', '5', '4', '1']);
    expect(el(fixture).querySelector('.report-completion__legend')?.textContent).toContain('5 of 10 completed');
    expect(el(fixture).querySelector('.progress__bar')).not.toBeNull();
    expect(getSummarySpy).toHaveBeenCalledWith({});
    expect(el(fixture).querySelector('h1')?.textContent).toContain('Task Reports');
  });

  it('applies the selected filters to the summary request', () => {
    const fixture = create();
    const component = fixture.componentInstance;

    fixture.componentInstance.setDeadlineFrom('2026-10-01');
    fixture.componentInstance.setDeadlineTo('2026-10-31');
    fixture.componentInstance.setAssignee('3');
    fixture.componentInstance.setDepartment('1');
    fixture.componentInstance.setStatus('Completed');
    fixture.componentInstance.setPriority('High');

    expect(lastFilter(fixture)).toEqual({
      deadlineFrom: startOfLocalDay('2026-10-01'),
      deadlineTo: endOfLocalDay('2026-10-31'),
      assignedToUserId: 3,
      departmentId: 1,
      status: 'Completed',
      priority: 'High'
    });

    fixture.componentInstance.resetFilters();
    expect(lastFilter(fixture)).toEqual({});
  });

  it('downloads the CSV for the current filter', () => {
    const createObjectUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:report');
    spyOn(URL, 'revokeObjectURL');
    const fixture = create();
    fixture.componentInstance.setStatus('Completed');

    (el(fixture).querySelector('[data-action="export"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(exportSpy).toHaveBeenCalledWith({ status: 'Completed' });
    expect(createObjectUrl).toHaveBeenCalled();
  });

  it('reports a failed export', () => {
    exportSpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create();

    (el(fixture).querySelector('[data-action="export"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(toastErrorSpy).toHaveBeenCalled();
  });

  it('shows zeros for an empty scope', () => {
    getSummarySpy.and.returnValue(of({ totalTasks: 0, pendingTasks: 0, completedTasks: 0, overdueTasks: 0 }));
    const fixture = create();

    const values = Array.from(el(fixture).querySelectorAll('.stat-card__value')).map((node) => node.textContent?.trim());
    expect(values).toEqual(['0', '0', '0', '0']);
    expect(el(fixture).querySelector('.report-completion__legend')?.textContent).toContain('0%');
  });

  it('shows the error state and retries', () => {
    getSummarySpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Something went wrong');

    getSummarySpy.and.returnValue(of(summary));
    (el(fixture).querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('.stat-card').length).toBe(4);
  });

  it('builds deadline bounds from local calendar days', () => {
    expect(startOfLocalDay('2026-10-15')).toBe(new Date(2026, 9, 15).toISOString());
    expect(endOfLocalDay('2026-10-15')).toBe(new Date(2026, 9, 15, 23, 59, 59, 999).toISOString());
  });
});
