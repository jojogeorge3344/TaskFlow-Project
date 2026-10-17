import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guard';
import { routes } from './app.routes';
import { ShellComponent } from './core/layout/shell.component';
import { AdminDashboardComponent } from './features/dashboard/admin-dashboard.component';
import { EmployeeDashboardComponent } from './features/dashboard/employee-dashboard.component';
import { ManagerDashboardComponent } from './features/dashboard/manager-dashboard.component';
import { DepartmentListComponent } from './features/departments/department-list.component';
import { EmployeeListComponent } from './features/employees/employee-list.component';
import { NotificationCenterComponent } from './features/notifications/notification-center.component';
import { ReminderSettingsComponent } from './features/settings/reminder-settings.component';
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

  it('wraps authenticated screens in the shell behind the auth guard', async () => {
    const shell = shellRoute();
    expect(shell.canActivate).toEqual([authGuard]);
    expect(await load(shell)).toBe(ShellComponent);
    expect(shell.children!.map((child) => child.path)).toEqual([
      'admin',
      'manager',
      'me',
      'departments',
      'employees',
      'notifications',
      'settings/reminders',
      'board',
      'tasks',
      'tasks/new',
      'tasks/:id',
      'tasks/:id/discussion',
      'tasks/:id/activity',
      'tasks/:id/reassign'
    ]);
  });

  it('protects the administrator dashboard for Administrators only', () => {
    const admin = childRoute('admin');
    expect(admin.canActivate).toEqual([roleGuard]);
    expect(admin.data?.['roles']).toEqual(['Administrator']);
  });

  it('protects the manager dashboard for Managers only', () => {
    const manager = childRoute('manager');
    expect(manager.canActivate).toEqual([roleGuard]);
    expect(manager.data?.['roles']).toEqual(['Manager']);
  });

  it('protects the employee dashboard at /me for Employees only', () => {
    const me = childRoute('me');
    expect(me.canActivate).toEqual([roleGuard]);
    expect(me.data?.['roles']).toEqual(['Employee']);
  });

  it('protects the management screens for Administrators only', () => {
    for (const path of ['employees', 'departments']) {
      const route = childRoute(path);
      expect(route.canActivate).toEqual([roleGuard]);
      expect(route.data?.['roles']).toEqual(['Administrator']);
    }
  });

  it('loads the role-specific dashboards and management screens', async () => {
    expect(await load(childRoute('admin'))).toBe(AdminDashboardComponent);
    expect(await load(childRoute('manager'))).toBe(ManagerDashboardComponent);
    expect(await load(childRoute('me'))).toBe(EmployeeDashboardComponent);
    expect(await load(childRoute('employees'))).toBe(EmployeeListComponent);
    expect(await load(childRoute('departments'))).toBe(DepartmentListComponent);
  });

  it('registers the notification center for every authenticated role', async () => {
    const notifications = childRoute('notifications');
    expect(notifications.canActivate).toBeUndefined();
    expect(await load(notifications)).toBe(NotificationCenterComponent);
  });

  it('protects reminder settings for Administrators and Managers only', async () => {
    const settings = childRoute('settings/reminders');
    expect(settings.canActivate).toEqual([roleGuard]);
    expect(settings.data?.['roles']).toEqual(['Administrator', 'Manager']);
    expect(settings.data?.['roles']).not.toContain('Employee');
    expect(await load(settings)).toBe(ReminderSettingsComponent);
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

  it('protects task creation for Managers and Administrators only, ahead of the detail route', async () => {
    const createRoute = childRoute('tasks/new');
    expect(createRoute.canActivate).toEqual([roleGuard]);
    expect(createRoute.data?.['roles']).toEqual(['Manager', 'Administrator']);
    expect(await load(createRoute)).toBe(TaskCreateComponent);

    const paths = shellRoute().children!.map((child) => child.path);
    expect(paths.indexOf('tasks/new')).toBeLessThan(paths.indexOf('tasks/:id'));
  });

  it('registers the task detail and discussion routes for every authenticated role', () => {
    expect(childRoute('tasks/:id').canActivate).toBeUndefined();
    expect(childRoute('tasks/:id/discussion').canActivate).toBeUndefined();
    expect(childRoute('tasks/:id/activity').canActivate).toBeUndefined();
  });

  it('protects reassignment for Managers and Administrators only', () => {
    const reassign = childRoute('tasks/:id/reassign');
    expect(reassign.canActivate).toEqual([roleGuard]);
    expect(reassign.data?.['roles']).toEqual(['Manager', 'Administrator']);
  });

  it('lazy-loads the task screens', async () => {
    expect(await load(childRoute('board'))).toBe(KanbanComponent);
    expect(await load(childRoute('tasks/:id'))).toBe(TaskDetailsComponent);
    expect(await load(childRoute('tasks/:id/discussion'))).toBe(TaskDiscussionComponent);
    expect(await load(childRoute('tasks/:id/activity'))).toBe(ActivityComponent);
    expect(await load(childRoute('tasks/:id/reassign'))).toBe(TaskReassignComponent);
  });
});
