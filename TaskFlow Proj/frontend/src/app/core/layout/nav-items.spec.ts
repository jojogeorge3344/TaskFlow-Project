import { NavItem, ROLE_NAVIGATION } from './nav-items';

function child(nav: NavItem[], label: string): NavItem {
  const item = nav.find((candidate) => candidate.label === label);
  expect(item).withContext(`nav item '${label}' should exist`).toBeDefined();
  return item!;
}

describe('ROLE_NAVIGATION', () => {
  describe('Administrator modules', () => {
    const admin = ROLE_NAVIGATION.Administrator;

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

    it('groups Reminder Settings and Application Settings under Settings', () => {
      const settings = child(admin, 'Settings');
      expect(settings.route)
        .withContext('the parent module itself is not a route')
        .toBeUndefined();
      expect(settings.children!.map((item) => item.label)).toEqual([
        'Reminder Settings',
        'Application Settings'
      ]);
      expect(settings.children!.map((item) => item.route)).toEqual([
        '/settings/reminders',
        '/settings/application'
      ]);
    });

    it('groups Mail and Notifications under Communication Management', () => {
      const communication = child(admin, 'Communication Management');
      expect(communication.route).toBeUndefined();
      expect(communication.children!.map((item) => item.label)).toEqual(['Mail', 'Notifications']);
      expect(communication.children!.map((item) => item.route)).toEqual(['/mail', '/notifications']);
      expect(child(communication.children!, 'Mail').badge).toBe('mail');
    });

    it('groups Employee Management and Department Management under TaskFlow Management', () => {
      const taskflow = child(admin, 'TaskFlow Management');
      expect(taskflow.route).toBeUndefined();
      expect(taskflow.children!.map((item) => item.label)).toEqual([
        'Employee Management',
        'Department Management'
      ]);
      expect(taskflow.children!.map((item) => item.route)).toEqual(['/employees', '/departments']);
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
    const manager = ROLE_NAVIGATION.Manager;

    it('groups Task Management, Employee Workload and Board under TaskFlow Monitoring', () => {
      const monitoring = child(manager, 'TaskFlow Monitoring');
      expect(monitoring.route).toBeUndefined();
      expect(monitoring.children!.map((item) => item.route)).toEqual(['/tasks', '/workload', '/board']);
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
        'Communication Management',
        'Reports',
        'Settings',
        'My Profile'
      ]);
    });
  });

  describe('Employee modules', () => {
    const employee = ROLE_NAVIGATION.Employee;

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
});
