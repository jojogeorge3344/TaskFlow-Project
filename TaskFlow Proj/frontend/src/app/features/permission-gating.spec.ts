import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { Permissions } from '../core/auth/permissions';
import { DepartmentService } from '../core/services/department.service';
import { ProjectService } from '../core/services/project.service';
import { ReportService } from '../core/services/report.service';
import { UserService } from '../core/services/user.service';
import { PositionService } from '../core/services/position.service';
import { ToastService } from '../shared/ui/toast.service';
import { fakeAuthService } from '../testing/permissions';
import { DepartmentListComponent } from './departments/department-list.component';
import { ProjectListComponent } from './projects/project-list.component';
import { ReportComponent } from './reports/report.component';
import { TaskService } from './tasks/task.service';
import { TaskDiscussionComponent } from './tasks/task-discussion.component';

/** Controls a role may not use are not drawn at all; the API refuses the same actions regardless. */
describe('hiding what a role may not use', () => {
  const as = (permissions: string[]) => ({ provide: AuthService, useValue: fakeAuthService(() => ({ permissions })) });

  const department = { id: 1, name: 'Engineering', managerCount: 0, employeeCount: 0, createdAt: '2026-01-01T00:00:00Z' };
  const project = { id: 1, name: 'Apollo', description: null, createdAt: '2026-01-01T00:00:00Z', employeeCount: 0, managers: [] };

  function departments(permissions: string[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [DepartmentListComponent],
      providers: [
        provideRouter([]),
        as(permissions),
        { provide: DepartmentService, useValue: { getAll: () => of([department]) } },
        { provide: UserService, useValue: { getAll: () => of([]) } },
        { provide: ToastService, useValue: { success: () => undefined, error: () => undefined } }
      ]
    });
    const fixture = TestBed.createComponent(DepartmentListComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows a department list without add, members, edit or delete to a role that may only view', () => {
    const page = departments([Permissions.DepartmentsView]);

    expect(page.textContent).toContain('Engineering');
    expect(page.querySelector('.tf-page-header button')).toBeNull();
    for (const action of ['members', 'edit', 'delete']) {
      expect(page.querySelector(`[data-action="${action}"]`)).withContext(action).toBeNull();
    }
  });

  it('shows only the department actions the role holds', () => {
    const page = departments([Permissions.DepartmentsView, Permissions.DepartmentsEdit]);

    expect(page.querySelector('[data-action="edit"]')).not.toBeNull();
    expect(page.querySelector('[data-action="delete"]')).toBeNull();
    expect(page.querySelector('[data-action="members"]')).toBeNull();
    expect(page.querySelector('.tf-page-header button')).toBeNull();
  });

  function projects(permissions: string[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [ProjectListComponent],
      providers: [
        provideRouter([]),
        as(permissions),
        { provide: ProjectService, useValue: { getAll: () => of([project]) } },
        { provide: PositionService, useValue: { getAll: () => of([]) } },
        { provide: UserService, useValue: { getAll: () => of([]) } }
      ]
    });
    const fixture = TestBed.createComponent(ProjectListComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows project actions one by one', () => {
    const viewOnly = projects([Permissions.ProjectsView]);
    expect(viewOnly.querySelector('.project__actions')).toBeNull();
    expect(viewOnly.querySelector('.tf-page-header button')).toBeNull();

    TestBed.resetTestingModule();
    const deleteOnly = projects([Permissions.ProjectsView, Permissions.ProjectsDelete]);
    expect(deleteOnly.querySelector('[data-action="delete"]')).not.toBeNull();
    expect(deleteOnly.querySelector('[data-action="edit"]')).toBeNull();
    expect(deleteOnly.querySelector('[data-action="team"]')).toBeNull();
  });

  function report(permissions: string[]): HTMLElement {
    TestBed.configureTestingModule({
      providers: [
        as(permissions),
        {
          provide: ReportService,
          useValue: { getSummary: () => of({ totalTasks: 0, pendingTasks: 0, completedTasks: 0, overdueTasks: 0 }), exportCsv: () => of(new Blob()) }
        },
        { provide: ProjectService, useValue: { getAll: () => of([]) } },
        { provide: TaskService, useValue: { getAssignees: () => of([]) } },
        { provide: ToastService, useValue: { error: () => undefined } }
      ]
    });
    const fixture = TestBed.createComponent(ReportComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('hides Export CSV from a role that may read reports but not export them', () => {
    expect(report([Permissions.ReportsView]).querySelector('[data-action="export"]')).toBeNull();

    TestBed.resetTestingModule();
    expect(report([Permissions.ReportsView, Permissions.ReportsExport]).querySelector('[data-action="export"]')).not.toBeNull();
  });

  function discussion(permissions: string[]): HTMLElement {
    TestBed.configureTestingModule({
      imports: [TaskDiscussionComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        {
          provide: AuthService,
          useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Custom', permissions }), hasPermission: (key: string) => permissions.includes(key) }
        },
        { provide: TaskService, useValue: { getComments: () => of([]), addComment: () => of(null) } },
        { provide: ToastService, useValue: { success: () => undefined, error: () => undefined } }
      ]
    });
    const fixture = TestBed.createComponent(TaskDiscussionComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('lets a role without the comment permission read a discussion but not write in it', () => {
    expect(discussion([]).querySelector('.discussion__composer')).toBeNull();

    TestBed.resetTestingModule();
    expect(discussion([Permissions.TasksCommentCreate]).querySelector('.discussion__composer')).not.toBeNull();
  });
});
