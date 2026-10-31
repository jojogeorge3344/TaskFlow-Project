import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, Routes, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { authGuard, mfaSetupGuard, permissionGuard, securitySetupGuard } from '../shared/auth/auth.guard';
import { DepartmentScreenPermissions, EmployeeScreenPermissions, Permissions } from '../shared/auth/permissions';
import { routes } from './app.routes';
import { expandRoutes } from '../shared/testing/routes';
import { ShellComponent } from '../layouts/shell.component';
import { LoginComponent } from '../modules/auth/login/pages/login.component';
import { LegalPageComponent } from '../modules/legal/pages/legal-page.component';
import { AdminDashboardComponent } from '../modules/dashboard/pages/admin-dashboard.component';
import { EmployeeDashboardComponent } from '../modules/dashboard/pages/employee-dashboard.component';
import { ManagerDashboardComponent } from '../modules/dashboard/pages/manager-dashboard.component';
import { ProjectListComponent } from '../modules/management/project-management/pages/project-list.component';
import { ProjectDetailComponent } from '../modules/management/project-management/pages/project-detail.component';
import { DepartmentListComponent } from '../modules/management/department-management/pages/department-list.component';
import { EmployeeListComponent } from '../modules/management/employee-management/pages/employee-list.component';
import { MailComponent } from '../modules/communication/mail/pages/mail.component';
import { NotificationCenterComponent } from '../modules/communication/notification/pages/notification-center.component';
import { ReportComponent } from '../modules/reports/pages/report.component';
import { PermissionsManagementComponent } from '../modules/settings/role-configuration/pages/permissions-management.component';
import { ReminderSettingsComponent } from '../modules/settings/reminder-settings/pages/reminder-settings.component';
import { TfaSettingsComponent } from '../modules/settings/tfa-settings/pages/tfa-settings.component';
import { WorkloadComponent } from '../modules/monitoring/employee-workload/pages/workload.component';
import { ActivityComponent } from '../modules/monitoring/task-management/activity/pages/activity.component';
import { KanbanComponent } from '../modules/monitoring/board/pages/kanban.component';
import { TaskReassignComponent } from '../modules/monitoring/task-management/pages/reassign.component';
import { TaskDetailsComponent } from '../modules/monitoring/task-management/pages/task-details.component';
import { TaskDiscussionComponent } from '../modules/monitoring/task-management/discussion/pages/task-discussion.component';
import { TaskCreateComponent } from '../modules/monitoring/task-management/pages/task-create.component';
import { TaskListComponent } from '../modules/monitoring/task-management/pages/task-list.component';

describe('app routes', () => {
  // Each module keeps its routes in its own file, loaded with loadChildren: these are the routes as the router sees them.
  let topLevel: Routes;
  let shellChildren: Routes;

  beforeAll(async () => {
    topLevel = await expandRoutes(routes);
    shellChildren = await expandRoutes(shellRoute().children!);
  });

  function shellRoute(): Routes[number] {
    const shell = routes.find((candidate) => candidate.path === '' && candidate.children !== undefined);
    expect(shell).withContext('shell route should exist').toBeDefined();
    return shell!;
  }

  function topLevelRoute(path: string): Routes[number] {
    const route = topLevel.find((candidate) => candidate.path === path && candidate.children === undefined);
    expect(route).withContext(`top-level route '${path}' should exist`).toBeDefined();
    return route!;
  }

  function childRoute(path: string): Routes[number] {
    const route = shellChildren.find((candidate) => candidate.path === path);
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

  it('keeps the public routes, their guards and data outside the shell', () => {
    const publicRoutes = topLevel
      .filter((route) => route.children === undefined && route.path !== '' && route.path !== '**')
      .map((route) => ({ path: route.path, canActivate: route.canActivate, data: route.data }));

    expect(publicRoutes).toEqual([
      { path: 'login', canActivate: undefined, data: undefined },
      { path: 'account-recovery', canActivate: undefined, data: undefined },
      { path: 'security-setup', canActivate: [authGuard, securitySetupGuard], data: undefined },
      { path: 'legal/copyright', canActivate: undefined, data: { documentId: 'copyright' } },
      { path: 'legal/terms', canActivate: undefined, data: { documentId: 'terms' } },
      { path: 'legal/privacy', canActivate: undefined, data: { documentId: 'privacy' } },
      { path: 'legal/disclaimer', canActivate: undefined, data: { documentId: 'disclaimer' } },
      { path: 'legal/licenses', canActivate: undefined, data: { documentId: 'licenses' } }
    ]);
    expect(topLevel[topLevel.length - 1]).toEqual({ path: '**', redirectTo: 'login' });
  });

  it('loads the login component outside the shell and without guards', () => {
    const login = topLevelRoute('login');
    expect(login.loadComponent).toBeDefined();
    expect(login.canActivate).toBeUndefined();
    expect(shellChildren.some((child) => child.path === 'login')).toBeFalse();
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
    expect(shell.canActivate).toEqual([authGuard, mfaSetupGuard]);
    expect(await load(shell)).toBe(ShellComponent);
    expect(shellChildren.map((child) => child.path).sort()).toEqual([
      'admin',
      'manager',
      'me',
      'projects',
      'projects/:id',
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
      'profile/change-password',
      'board',
      'tasks',
      'tasks/new',
      'tasks/:id',
      'tasks/:id/discussion',
      'tasks/:id/activity',
      'tasks/:id/reassign'
    ].sort());
  });

  it('protects the administrator dashboard with dashboard.view.organization', () => {
    const admin = childRoute('admin');
    expect(admin.canActivate).toEqual([permissionGuard]);
    expect(admin.data?.['permissions']).toEqual([Permissions.DashboardViewOrganization]);
  });

  it('protects the manager dashboard with dashboard.view.team', () => {
    const manager = childRoute('manager');
    expect(manager.canActivate).toEqual([permissionGuard]);
    expect(manager.data?.['permissions']).toEqual([Permissions.DashboardViewTeam]);
  });

  it('opens the personal dashboard at /me to every signed-in user, custom roles included', () => {
    const me = childRoute('me');
    expect(me.canActivate).toBeUndefined();
  });

  it('protects the management screens with their view or manage permission', () => {
    const expected: Record<string, string[]> = {
      employees: EmployeeScreenPermissions,
      projects: [Permissions.ProjectsView],
      'projects/:id': [Permissions.ProjectsView],
      departments: DepartmentScreenPermissions
    };
    for (const path of ['employees', 'projects', 'projects/:id', 'departments']) {
      const route = childRoute(path);
      expect(route.canActivate).toEqual([permissionGuard]);
      expect(route.data?.['permissions']).toEqual(expected[path]);
    }
  });

  it('loads the role-specific dashboards and management screens', async () => {
    expect(await load(childRoute('admin'))).toBe(AdminDashboardComponent);
    expect(await load(childRoute('manager'))).toBe(ManagerDashboardComponent);
    expect(await load(childRoute('me'))).toBe(EmployeeDashboardComponent);
    expect(await load(childRoute('employees'))).toBe(EmployeeListComponent);
    expect(await load(childRoute('projects'))).toBe(ProjectListComponent);
    expect(await load(childRoute('projects/:id'))).toBe(ProjectDetailComponent);
    expect(await load(childRoute('departments'))).toBe(DepartmentListComponent);
  });

  it('protects the notification center with notifications.view', async () => {
    const notifications = childRoute('notifications');
    expect(notifications.canActivate).toEqual([permissionGuard]);
    expect(notifications.data?.['permissions']).toEqual([Permissions.NotificationsView]);
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

  it('protects TFA Settings with settings.ai.view', async () => {
    const route = childRoute('settings/tfa');
    expect(route.canActivate).toEqual([permissionGuard]);
    expect(route.data?.['permissions']).toEqual([Permissions.SettingsAiView]);
    expect(await load(route)).toBe(TfaSettingsComponent);
  });

  it('protects Permissions Management with roles.manage, which only the Administrator role holds', async () => {
    const route = childRoute('settings/permissions');
    expect(route.canActivate).toEqual([permissionGuard]);
    expect(route.data?.['permissions']).toEqual([Permissions.RolesManage]);
    expect(await load(route)).toBe(PermissionsManagementComponent);
  });

  it('protects Mail with mail.read', async () => {
    const mail = childRoute('mail');
    expect(mail.canActivate).toEqual([permissionGuard]);
    expect(mail.data?.['permissions']).toEqual([Permissions.MailRead]);
    expect(await load(mail)).toBe(MailComponent);
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

    const paths = shellChildren.map((child) => child.path);
    expect(paths.indexOf('tasks/new')).toBeLessThan(paths.indexOf('tasks/:id'));
  });

  it('registers the task detail and discussion routes for every authenticated role', () => {
    expect(childRoute('tasks/:id').canActivate).toBeUndefined();
    expect(childRoute('tasks/:id/discussion').canActivate).toBeUndefined();
  });

  it('protects reassignment with tasks.reassign', () => {
    const reassign = childRoute('tasks/:id/reassign');
    expect(reassign.canActivate).toEqual([permissionGuard]);
    expect(reassign.data?.['permissions']).toEqual([Permissions.TasksReassign]);
  });

  it('opens the board only with board.view', () => {
    const board = childRoute('board');
    expect(board.canActivate).toEqual([permissionGuard]);
    expect(board.data?.['permissions']).toEqual([Permissions.BoardView]);
  });

  it('opens a task activity history only with tasks.activity.view', () => {
    const activity = childRoute('tasks/:id/activity');
    expect(activity.canActivate).toEqual([permissionGuard]);
    expect(activity.data?.['permissions']).toEqual([Permissions.TasksActivityView]);
  });

  it('lazy-loads the task screens', async () => {
    expect(await load(childRoute('board'))).toBe(KanbanComponent);
    expect(await load(childRoute('tasks/:id'))).toBe(TaskDetailsComponent);
    expect(await load(childRoute('tasks/:id/discussion'))).toBe(TaskDiscussionComponent);
    expect(await load(childRoute('tasks/:id/activity'))).toBe(ActivityComponent);
    expect(await load(childRoute('tasks/:id/reassign'))).toBe(TaskReassignComponent);
  });
  it('loads each module through a bare empty-path entry, so the module file holds every guard and data value', () => {
    const wrappers = [...routes, ...shellRoute().children!].filter((route) => route.loadChildren !== undefined);

    expect(wrappers.length).toBe(9);
    for (const wrapper of wrappers) {
      expect(Object.keys(wrapper).sort()).withContext(String(wrapper.loadChildren)).toEqual(['loadChildren', 'path']);
      expect(wrapper.path).toBe('');
    }
  });
});

describe('app routes in a real router', () => {
  async function navigate(url: string): Promise<{ url: string; component: unknown }> {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()] });
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl(url);
    return { url: TestBed.inject(Router).url, component: (component as object).constructor };
  }

  it('opens the sign-in page from the root and from an unknown address', async () => {
    expect(await navigate('/')).toEqual({ url: '/login', component: LoginComponent });
    TestBed.resetTestingModule();
    expect(await navigate('/no-such-page')).toEqual({ url: '/login', component: LoginComponent });
  });

  it('opens a legal page without signing in, past the auth module routes', async () => {
    expect(await navigate('/legal/terms')).toEqual({ url: '/legal/terms', component: LegalPageComponent });
  });

  it('keeps a signed-out visitor out of the shell', async () => {
    const result = await navigate('/tasks');
    expect(result.url).not.toBe('/tasks');
  });
});

