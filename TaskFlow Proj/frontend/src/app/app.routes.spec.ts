import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from './core/auth/auth.guard';
import { Permissions } from './core/auth/permissions';
import { routes } from './app.routes';
import { ShellComponent } from './core/layout/shell.component';
import { AdminDashboardComponent } from './features/dashboard/admin-dashboard.component';
import { EmployeeDashboardComponent } from './features/dashboard/employee-dashboard.component';
import { ManagerDashboardComponent } from './features/dashboard/manager-dashboard.component';
import { ProjectListComponent } from './features/projects/project-list.component';
import { DepartmentListComponent } from './features/departments/department-list.component';
import { EmployeeListComponent } from './features/employees/employee-list.component';
import { MailComponent } from './features/mail/mail.component';
import { NotificationCenterComponent } from './features/notifications/notification-center.component';
import { ReportComponent } from './features/reports/report.component';
import { PermissionsManagementComponent } from './features/settings/permissions-management.component';
import { ReminderSettingsComponent } from './features/settings/reminder-settings.component';
import { TfaSettingsComponent } from './features/settings/tfa-settings.component';
import { WorkloadComponent } from './features/workload/workload.component';
import { ActivityComponent } from './features/tasks/activity.component';
import { KanbanComponent } from './features/tasks/kanban.component';
import { TaskReassignComponent } from './features/tasks/reassign.component';
import { TaskDetailsComponent } from './features/tasks/task-details.component';
import { TaskDiscussionComponent } from './features/tasks/task-discussion.component';
import { TaskCreateComponent } from './features/tasks/task-create.component';
import { TaskListComponent } from './features/tasks/task-list.component';

describe('app routes', () => {
  function shellRoute(): Routes[number] {
    const shell = routes.find((candidate) => candidate.path === '' && candidate.children !== undefined);
    expect(shell).withContext('shell route should exist').toBeDefined();
    return shell!;
  }

  function topLevelRoute(path: string): Routes[number] {
    const route = routes.find((candidate) => candidate.path === path && candidate.children === undefined);
    expect(route).withContext(`top-level route '${path}' should exist`).toBeDefined();
    return route!;
  }

  function childRoute(path: string): Routes[number] {
    const route = shellRoute().children!.find((candidate) => candidate.path === path);
    expect(route).withContext(`shell child route '${path}' should exist`).toBeDefined();
    return route!;
  }

  async function load(route: Routes[number]): Promise<unknown> {
    const loadComponent = route.loadComponent as () => Promise<unknown>;
    return loadComponent();
  }

  it('redirects the empty path to the login screen', () => {
    const root = topLevelRoute('');
    expect(root.redirectTo).toBe('login');
    expect(root.pathMatch).toBe('full');
  });

  it('loads the login component outside the shell and without guards', () => {
    const login = topLevelRoute('login');
    expect(login.loadComponent).toBeDefined();
    expect(login.canActivate).toBeUndefined();
    expect(shellRoute().children!.some((child) => child.path === 'login')).toBeFalse();
  });

  it('keeps the TFA assistant out of the login screen: it is part of the guarded shell only', async () => {
    const login = topLevelRoute('login');
    const loginComponent = await login.loadComponent!();
    const shell = shellRoute();

    expect(shell.canActivate).toContain(authGuard);
    expect(login.canActivate).toBeUndefined();
    expect(login.children).toBeUndefined();
    expect(loginComponent).not.toBe(ShellComponent);
  });

  it('wraps authenticated screens in the shell behind the auth guard', async () => {
    const shell = shellRoute();
    expect(shell.canActivate).toEqual([authGuard]);
    expect(await load(shell)).toBe(ShellComponent);
    expect(shell.children!.map((child) => child.path)).toEqual([
      'admin',
      'manager',
      'me',
      'projects',
      'departments',
      'employees',
      'workload',
      'reports',
      'mail',
      'notifications',
      'settings/reminders',
      'settings/application',
      'settings/tfa',
      'settings/permissions',
      'profile',
      'board',
      'tasks',
      'tasks/new',
      'tasks/:id',
      'tasks/:id/discussion',
      'tasks/:id/activity',
      'tasks/:id/reassign'
    ]);
  });

  it('protects the administrator dashboard with dashboard.admin', () => {
    const admin = childRoute('admin');
    expect(admin.canActivate).toEqual([permissionGuard]);
    expect(admin.data?.['permissions']).toEqual([Permissions.DashboardAdmin]);
  });

  it('protects the manager dashboard with dashboard.manager', () => {
    const manager = childRoute('manager');
    expect(manager.canActivate).toEqual([permissionGuard]);
    expect(manager.data?.['permissions']).toEqual([Permissions.DashboardManager]);
  });

  it('opens the personal dashboard at /me to every signed-in user, custom roles included', () => {
    const me = childRoute('me');
    expect(me.canActivate).toBeUndefined();
  });

  it('protects the management screens with their view or manage permission', () => {
    const expected: Record<string, string> = {
      employees: Permissions.EmployeesManage,
      projects: Permissions.ProjectsView,
      departments: Permissions.DepartmentsManage
    };
    for (const path of ['employees', 'projects', 'departments']) {
      const route = childRoute(path);
      expect(route.canActivate).toEqual([permissionGuard]);
      expect(route.data?.['permissions']).toEqual([expected[path]]);
    }
  });

  it('loads the role-specific dashboards and management screens', async () => {
    expect(await load(childRoute('admin'))).toBe(AdminDashboardComponent);
    expect(await load(childRoute('manager'))).toBe(ManagerDashboardComponent);
    expect(await load(childRoute('me'))).toBe(EmployeeDashboardComponent);
    expect(await load(childRoute('employees'))).toBe(EmployeeListComponent);
    expect(await load(childRoute('projects'))).toBe(ProjectListComponent);
    expect(await load(childRoute('departments'))).toBe(DepartmentListComponent);
  });

  it('registers the notification center for every authenticated role', async () => {
    const notifications = childRoute('notifications');
    expect(notifications.canActivate).toBeUndefined();
    expect(await load(notifications)).toBe(NotificationCenterComponent);
  });

  it('protects the workload and reports screens with workload.view and reports.view', async () => {
    const expected: Record<string, string> = { workload: Permissions.WorkloadView, reports: Permissions.ReportsView };
    for (const path of ['workload', 'reports']) {
      const route = childRoute(path);
      expect(route.canActivate).toEqual([permissionGuard]);
      expect(route.data?.['permissions']).toEqual([expected[path]]);
    }
    expect(await load(childRoute('workload'))).toBe(WorkloadComponent);
    expect(await load(childRoute('reports'))).toBe(ReportComponent);
  });

  it('protects reminder settings with settings.reminders.manage', async () => {
    const settings = childRoute('settings/reminders');
    expect(settings.canActivate).toEqual([permissionGuard]);
    expect(settings.data?.['permissions']).toEqual([Permissions.SettingsReminders]);
    expect(await load(settings)).toBe(ReminderSettingsComponent);
  });

  it('protects TFA Settings with settings.ai.manage, which only the Administrator role holds', async () => {
    const route = childRoute('settings/tfa');
    expect(route.canActivate).toEqual([permissionGuard]);
    expect(route.data?.['permissions']).toEqual([Permissions.SettingsAi]);
    expect(await load(route)).toBe(TfaSettingsComponent);
  });

  it('protects Permissions Management with roles.manage, which only the Administrator role holds', async () => {
    const route = childRoute('settings/permissions');
    expect(route.canActivate).toEqual([permissionGuard]);
    expect(route.data?.['permissions']).toEqual([Permissions.RolesManage]);
    expect(await load(route)).toBe(PermissionsManagementComponent);
  });

  it('registers Mail for every signed-in user', async () => {
    const mail = childRoute('mail');
    expect(mail.canActivate).toBeUndefined();
    expect(await load(mail)).toBe(MailComponent);
  });

  it('registers the board for every authenticated role', () => {
    const board = childRoute('board');
    expect(board.canActivate).toBeUndefined();
  });

  it('registers the task list for every authenticated role', async () => {
    const list = childRoute('tasks');
    expect(list.canActivate).toBeUndefined();
    expect(list.pathMatch).toBe('full');
    expect(await load(list)).toBe(TaskListComponent);
  });

  it('protects task creation with tasks.create, ahead of the detail route', async () => {
    const createRoute = childRoute('tasks/new');
    expect(createRoute.canActivate).toEqual([permissionGuard]);
    expect(createRoute.data?.['permissions']).toEqual([Permissions.TasksCreate]);
    expect(await load(createRoute)).toBe(TaskCreateComponent);

    const paths = shellRoute().children!.map((child) => child.path);
    expect(paths.indexOf('tasks/new')).toBeLessThan(paths.indexOf('tasks/:id'));
  });

  it('registers the task detail and discussion routes for every authenticated role', () => {
    expect(childRoute('tasks/:id').canActivate).toBeUndefined();
    expect(childRoute('tasks/:id/discussion').canActivate).toBeUndefined();
    expect(childRoute('tasks/:id/activity').canActivate).toBeUndefined();
  });

  it('protects reassignment with tasks.reassign', () => {
    const reassign = childRoute('tasks/:id/reassign');
    expect(reassign.canActivate).toEqual([permissionGuard]);
    expect(reassign.data?.['permissions']).toEqual([Permissions.TasksReassign]);
  });

  it('lazy-loads the task screens', async () => {
    expect(await load(childRoute('board'))).toBe(KanbanComponent);
    expect(await load(childRoute('tasks/:id'))).toBe(TaskDetailsComponent);
    expect(await load(childRoute('tasks/:id/discussion'))).toBe(TaskDiscussionComponent);
    expect(await load(childRoute('tasks/:id/activity'))).toBe(ActivityComponent);
    expect(await load(childRoute('tasks/:id/reassign'))).toBe(TaskReassignComponent);
  });
});
