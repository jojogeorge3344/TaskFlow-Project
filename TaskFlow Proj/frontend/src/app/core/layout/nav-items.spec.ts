import { Permissions } from '../auth/permissions';
import { systemPermissions } from '../../testing/permissions';
import { NavItem, buildNavigation } from './nav-items';

function navFor(role: string, permissions: string[] = systemPermissions(role)): NavItem[] {
  return buildNavigation({ role, permissions });
}

function child(nav: NavItem[], label: string): NavItem {
  const item = nav.find((candidate) => candidate.label === label);
  expect(item).withContext(`nav item '${label}' should exist`).toBeDefined();
  return item!;
}

describe('buildNavigation', () => {
  describe('Administrator modules', () => {
    const admin = navFor('Administrator');

    it('groups Task Management, Employee Workload and Board under TaskFlow Monitoring', () => {
      const monitoring = child(admin, 'TaskFlow Monitoring');
      expect(monitoring.route).toBeUndefined();
      expect(monitoring.children!.map((item) => item.label)).toEqual([
        'Task Management',
        'Employee Workload',
        'Board'
      ]);
      expect(monitoring.children!.map((item) => item.route)).toEqual(['/tasks', '/workload', '/board']);
      expect(child(monitoring.children!, 'Board').icon).toBe('kanban');
    });

    it('groups Reminder Settings, Application Settings, TFA Settings and Role Configuration under Settings', () => {
      const settings = child(admin, 'Settings');
      expect(settings.route)
        .withContext('the parent module itself is not a route')
        .toBeUndefined();
      expect(settings.children!.map((item) => item.label)).toEqual([
        'Reminder Settings',
        'Application Settings',
        'TFA Settings',
        'Role Configuration'
      ]);
      expect(settings.children!.map((item) => item.route)).toEqual([
        '/settings/reminders',
        '/settings/application',
        '/settings/tfa',
        '/settings/permissions'
      ]);
    });

    it('groups Mail and Notifications under Communication Management', () => {
      const communication = child(admin, 'Communication Management');
      expect(communication.route).toBeUndefined();
      expect(communication.children!.map((item) => item.label)).toEqual(['Mail', 'Notifications']);
      expect(communication.children!.map((item) => item.route)).toEqual(['/mail', '/notifications']);
      expect(child(communication.children!, 'Mail').badge).toBe('mail');
    });

    it('groups Employee Management, Project Management and Department Management under TaskFlow Management', () => {
      const taskflow = child(admin, 'TaskFlow Management');
      expect(taskflow.route).toBeUndefined();
      expect(taskflow.children!.map((item) => item.label)).toEqual([
        'Employee Management',
        'Project Management',
        'Department Management'
      ]);
      expect(taskflow.children!.map((item) => item.route)).toEqual(['/employees', '/projects', '/departments']);
    });

    it('keeps Dashboard, Reports and My Profile top-level and orders the sidebar', () => {
      expect(admin.map((item) => item.label)).toEqual([
        'Dashboard',
        'TaskFlow Monitoring',
        'TaskFlow Management',
        'Communication Management',
        'Reports',
        'Settings',
        'My Profile'
      ]);
    });

    it('exposes no route on any grouped child', () => {
      const grouped = admin.filter((item) => item.children);
      expect(grouped.length).toBe(4);
      for (const parent of grouped) {
        for (const entry of parent.children!) {
          expect(entry.route).withContext(`${parent.label} > ${entry.label}`).toBeDefined();
          expect(entry.children)
            .withContext(`${parent.label} > ${entry.label} has no deeper nesting`)
            .toBeUndefined();
        }
      }
    });
  });

  describe('Manager modules', () => {
    const manager = navFor('Manager');

    it('groups Task Management, Employee Workload and Board under TaskFlow Monitoring', () => {
      const monitoring = child(manager, 'TaskFlow Monitoring');
      expect(monitoring.route).toBeUndefined();
      expect(monitoring.children!.map((item) => item.route)).toEqual(['/tasks', '/workload', '/board']);
    });

    it('shows the read-only Project Management screen and no Department Management', () => {
      const taskflow = child(manager, 'TaskFlow Management');
      expect(taskflow.route).toBeUndefined();
      expect(taskflow.children!.map((item) => item.label)).toEqual(['Project Management']);
      expect(taskflow.children!.map((item) => item.route)).toEqual(['/projects']);
    });

    it('groups Mail and Notifications under Communication Management', () => {
      const communication = child(manager, 'Communication Management');
      expect(communication.children!.map((item) => item.label)).toEqual(['Mail', 'Notifications']);
      expect(child(communication.children!, 'Mail').badge).toBe('mail');
    });

    it('groups Reminder Settings under Settings', () => {
      const settings = child(manager, 'Settings');
      expect(settings.children!.map((item) => item.label)).toEqual(['Reminder Settings']);
      expect(settings.children!.map((item) => item.route)).toEqual(['/settings/reminders']);
    });

    it('keeps Dashboard, Reports and My Profile top-level and orders the sidebar', () => {
      expect(manager.map((item) => item.label)).toEqual([
        'Dashboard',
        'TaskFlow Monitoring',
        'TaskFlow Management',
        'Communication Management',
        'Reports',
        'Settings',
        'My Profile'
      ]);
    });
  });

  describe('Employee modules', () => {
    const employee = navFor('Employee');

    it('groups Mail and Notifications under Communication Management', () => {
      const communication = child(employee, 'Communication Management');
      expect(communication.route).toBeUndefined();
      expect(communication.children!.map((item) => item.label)).toEqual(['Mail', 'Notifications']);
      expect(child(communication.children!, 'Mail').badge).toBe('mail');
    });

    it('groups My Tasks and Board under Task Management', () => {
      const tasks = child(employee, 'Task Management');
      expect(tasks.route).toBeUndefined();
      expect(tasks.children!.map((item) => item.label)).toEqual(['My Tasks', 'Board']);
      expect(tasks.children!.map((item) => item.route)).toEqual(['/tasks', '/board']);
    });

    it('keeps Dashboard and My Profile top-level and orders the sidebar', () => {
      expect(employee.map((item) => item.label)).toEqual([
        'Dashboard',
        'Communication Management',
        'Task Management',
        'My Profile'
      ]);
    });
  });

  describe('Role Configuration entry', () => {
    it('is shown to the Administrator only', () => {
      const entry = (nav: NavItem[]) =>
        nav.find((item) => item.label === 'Settings')?.children?.find((item) => item.label === 'Role Configuration');

      expect(entry(navFor('Administrator'))?.route).toBe('/settings/permissions');
      expect(entry(navFor('Manager'))).toBeUndefined();
      expect(entry(navFor('Employee'))).toBeUndefined();
    });
  });

  describe('custom roles', () => {
    it('shows nothing the role has no permission for', () => {
      const nav = navFor('Team Lead', [Permissions.TasksCreate]);

      expect(nav.map((item) => item.label)).toEqual(['Dashboard', 'Task Management', 'My Profile']);
      expect(nav[0].route).toBe('/me');
      expect(child(nav, 'Task Management').children!.map((item) => item.label)).toEqual(['My Tasks', 'Board']);
    });

    it('adds the menu entries the permissions of the role open', () => {
      const nav = navFor('Analyst', [
        Permissions.TasksViewAll,
        Permissions.ReportsView,
        Permissions.WorkloadView,
        Permissions.SettingsReminders,
        Permissions.ProjectsView,
        Permissions.MailRead
      ]);

      expect(nav.map((item) => item.label)).toEqual([
        'Dashboard',
        'TaskFlow Monitoring',
        'TaskFlow Management',
        'Communication Management',
        'Reports',
        'Settings',
        'My Profile'
      ]);
      expect(child(nav, 'TaskFlow Monitoring').children!.map((item) => item.label)).toEqual(['Task Management', 'Employee Workload', 'Board']);
      expect(child(nav, 'TaskFlow Management').children!.map((item) => item.label)).toEqual(['Project Management']);
      expect(child(nav, 'Settings').children!.map((item) => item.label)).toEqual(['Reminder Settings']);
    });

    it('shows Mail and Notifications only to a role that holds them', () => {
      const entries = (permissions: string[]) =>
        navFor('Team Lead', permissions).find((item) => item.label === 'Communication Management')?.children?.map((item) => item.label);

      expect(entries([])).toBeUndefined();
      expect(entries([Permissions.MailRead])).toEqual(['Mail']);
      expect(entries([Permissions.NotificationsView])).toEqual(['Notifications']);
      expect(entries([Permissions.MailRead, Permissions.NotificationsView])).toEqual(['Mail', 'Notifications']);
    });

    it('lists the workload under Task Management for a role that cannot see every task', () => {
      const nav = navFor('Planner', [Permissions.WorkloadView]);

      expect(child(nav, 'Task Management').children!.map((item) => item.label)).toEqual(['My Tasks', 'Employee Workload', 'Board']);
    });

    it('sends a custom role to the widest dashboard its scope allows', () => {
      const dashboardOf = (permissions: string[]) => child(navFor('Auditor', permissions), 'Dashboard').route;

      expect(dashboardOf([])).toBe('/me');
      expect(dashboardOf([Permissions.DashboardViewTeam])).toBe('/manager');
      expect(dashboardOf([Permissions.DashboardViewOrganization])).toBe('/admin');
    });

    it('opens Employee Management only for a role that can act on employees, not just see them', () => {
      const labels = (permissions: string[]) =>
        navFor('Auditor', permissions)
          .flatMap((item) => item.children ?? [])
          .map((item) => item.label);

      expect(labels([Permissions.EmployeesView])).not.toContain('Employee Management');
      expect(labels([Permissions.EmployeesView, Permissions.EmployeesEdit])).toContain('Employee Management');
      expect(labels([Permissions.DepartmentsView, Permissions.DepartmentsMembers])).toContain('Department Management');
    });

    it('builds no menu without a user', () => {
      expect(buildNavigation(null)).toEqual([]);
    });
  });
});
