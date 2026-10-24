import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { ProjectWithCount, EmployeeWorkload } from '../../core/models';
import { ProjectService } from '../../core/services/project.service';
import { WorkloadService } from '../../core/services/workload.service';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { WorkloadComponent } from './workload.component';

describe('WorkloadComponent', () => {
  function employee(overrides: Partial<EmployeeWorkload>): EmployeeWorkload {
    return {
      userId: 1,
      fullName: 'Joe Thomas',
      projects: [{ projectId: 1, projectName: 'Engineering', positionId: 1, positionName: 'Member', percent: 100 }],
      allocatedPercent: 100,
      activeTasks: 1,
      highPriorityTasks: 0,
      overdueTasks: 0,
      completedTasks: 0,
      ...overrides
    };
  }

  const projects: ProjectWithCount[] = [
    { id: 1, name: 'Engineering', description: null, createdAt: '2026-01-01T00:00:00Z', employeeCount: 2, managers: [] },
    { id: 2, name: 'Sales', description: null, createdAt: '2026-01-01T00:00:00Z', employeeCount: 1, managers: [] }
  ];

  let getWorkloadSpy: jasmine.Spy;
  let getAllSpy: jasmine.Spy;
  let permissions: string[];

  function create(): ComponentFixture<WorkloadComponent> {
    const fixture = TestBed.createComponent(WorkloadComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<WorkloadComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    permissions = systemPermissions('Manager');
    getWorkloadSpy = jasmine.createSpy('getWorkload').and.returnValue(
      of([
        employee({ userId: 1, fullName: 'Joe Thomas', activeTasks: 1 }),
        employee({ userId: 2, fullName: 'Maya Manager', projects: [{ projectId: 2, projectName: 'Sales', positionId: 1, positionName: 'Member', percent: 60 }], allocatedPercent: 60, activeTasks: 1 }),
        employee({ userId: 3, fullName: 'Sam Overloaded', activeTasks: 4, overdueTasks: 1, highPriorityTasks: 2, completedTasks: 3 })
      ])
    );
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(projects));

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: WorkloadService, useValue: { getWorkload: getWorkloadSpy } },
        { provide: ProjectService, useValue: { getAll: getAllSpy } },
        { provide: AuthService, useValue: fakeAuthService(() => ({ permissions })) }
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

  it('shows every project of a person with its percentage, and the total allocated', () => {
    getWorkloadSpy.and.returnValue(
      of([
        employee({
          userId: 4,
          fullName: 'Two Projects',
          projects: [
            { projectId: 1, projectName: 'Apollo', positionId: 3, positionName: 'QA', percent: 60 },
            { projectId: 2, projectName: 'Zephyr', positionId: 4, positionName: 'Developer', percent: 40 }
          ],
          allocatedPercent: 100
        }),
        employee({ userId: 5, fullName: 'No Project', projects: [], allocatedPercent: 0 })
      ])
    );
    const fixture = create();

    const rows = el(fixture).querySelectorAll('tr.workload-row');
    expect(rows[0].querySelector('[data-field="projects"]')?.textContent?.replace(/\s+/g, ' ').trim()).toBe('Apollo 60%, Zephyr 40%');
    expect(rows[0].querySelector('[data-field="allocated"]')?.textContent?.trim()).toBe('100%');
    expect(rows[1].querySelector('[data-field="projects"]')?.textContent?.trim()).toBe('—');
    expect(rows[1].querySelector('[data-field="allocated"]')?.textContent?.trim()).toBe('0%');
  });

  it('filters the workload by project', () => {
    const fixture = create();

    fixture.componentInstance.setProject('2');

    expect(getWorkloadSpy).toHaveBeenCalledWith(2);

    fixture.componentInstance.setProject('');

    expect(getWorkloadSpy).toHaveBeenCalledWith(null);
    expect(getAllSpy).toHaveBeenCalled();
  });

  it('narrows the workload to a single employee', () => {
    const fixture = create();

    fixture.componentInstance.setEmployee('3');
    fixture.detectChanges();

    const rows = el(fixture).querySelectorAll('tr.workload-row');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Sam Overloaded');
    expect(el(fixture).textContent).toContain('1 employee');

    fixture.componentInstance.setEmployee('');
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('tr.workload-row').length).toBe(3);
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

  it('hides the filters without the workload filters permission', () => {
    permissions = [Permissions.WorkloadView, Permissions.WorkloadTasksOpen];
    const fixture = create();

    expect(el(fixture).querySelector('.workload-toolbar')).toBeNull();
    expect(el(fixture).querySelectorAll('tr.workload-row').length).toBe(3);
  });

  it('shows the filters with the workload filters permission', () => {
    permissions = [Permissions.WorkloadView, Permissions.WorkloadFilters];
    const fixture = create();

    expect(el(fixture).querySelector('.workload-toolbar')).not.toBeNull();
  });

  it('hides the task list link without the open-tasks permission', () => {
    permissions = [Permissions.WorkloadView, Permissions.WorkloadFilters];
    const fixture = create();

    expect(el(fixture).querySelector('a[data-open-tasks]')).toBeNull();
    expect(el(fixture).querySelectorAll('tr.workload-row').length).toBe(3);
    expect(el(fixture).querySelectorAll('thead th').length).toBe(8);
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
