import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DepartmentWithCount, EmployeeWorkload } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { WorkloadService } from '../../core/services/workload.service';
import { WorkloadComponent } from './workload.component';

describe('WorkloadComponent', () => {
  function employee(overrides: Partial<EmployeeWorkload>): EmployeeWorkload {
    return {
      userId: 1,
      fullName: 'Joe Thomas',
      departmentId: 1,
      departmentName: 'Engineering',
      activeTasks: 1,
      highPriorityTasks: 0,
      overdueTasks: 0,
      completedTasks: 0,
      ...overrides
    };
  }

  const departments: DepartmentWithCount[] = [
    { id: 1, name: 'Engineering', description: null, createdAt: '2026-01-01T00:00:00Z', employeeCount: 2 },
    { id: 2, name: 'Sales', description: null, createdAt: '2026-01-01T00:00:00Z', employeeCount: 1 }
  ];

  let getWorkloadSpy: jasmine.Spy;
  let getAllSpy: jasmine.Spy;

  function create(): ComponentFixture<WorkloadComponent> {
    const fixture = TestBed.createComponent(WorkloadComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<WorkloadComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    getWorkloadSpy = jasmine.createSpy('getWorkload').and.returnValue(
      of([
        employee({ userId: 1, fullName: 'Joe Thomas', activeTasks: 1 }),
        employee({ userId: 2, fullName: 'Maya Manager', departmentId: 2, departmentName: 'Sales', activeTasks: 1 }),
        employee({ userId: 3, fullName: 'Sam Overloaded', activeTasks: 4, overdueTasks: 1, highPriorityTasks: 2, completedTasks: 3 })
      ])
    );
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(departments));

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: WorkloadService, useValue: { getWorkload: getWorkloadSpy } },
        { provide: DepartmentService, useValue: { getAll: getAllSpy } }
      ]
    });
  });

  it('renders one row per employee with counts, a workload bar and a summary', () => {
    const fixture = create();
    const rows = el(fixture).querySelectorAll('tr.workload-row');

    expect(rows.length).toBe(3);
    expect(rows[2].textContent).toContain('Sam Overloaded');
    expect(rows[2].textContent).toContain('4');
    expect(rows[2].querySelector('.workload-bar__fill')).not.toBeNull();
    expect(el(fixture).querySelector('h1')?.textContent).toContain('Employee Workload');
    expect(el(fixture).textContent).toContain('3 employees');
    expect(getWorkloadSpy).toHaveBeenCalledWith(null);
  });

  it('filters the workload by department', () => {
    const fixture = create();

    fixture.componentInstance.setDepartment('2');

    expect(getWorkloadSpy).toHaveBeenCalledWith(2);

    fixture.componentInstance.setDepartment('');

    expect(getWorkloadSpy).toHaveBeenCalledWith(null);
    expect(getAllSpy).toHaveBeenCalled();
  });

  it('highlights employees at or above twice the average active count', () => {
    const fixture = create();
    const rows = el(fixture).querySelectorAll('tr.workload-row');

    // Active counts 1, 1, 4: average 2, so the 4-task employee is "high load" and the others are not.
    expect(rows[2].classList).toContain('workload-row--high');
    expect(rows[2].querySelector('[data-high-load]')).not.toBeNull();
    expect(rows[0].classList).not.toContain('workload-row--high');
    expect(rows[0].querySelector('[data-high-load]')).toBeNull();
  });

  it('links each row into the task list filtered by that employee', () => {
    const fixture = create();
    const rows = el(fixture).querySelectorAll('tr.workload-row');

    expect(rows[0].querySelector('a[data-open-tasks]')?.getAttribute('href')).toBe('/tasks?assignedToUserId=1');
    expect(rows[2].querySelector('a[data-open-tasks]')?.getAttribute('href')).toBe('/tasks?assignedToUserId=3');
  });

  it('shows the error state and retries', () => {
    getWorkloadSpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Something went wrong');

    getWorkloadSpy.and.returnValue(of([employee({})]));
    (el(fixture).querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('tr.workload-row').length).toBe(1);
  });
});
