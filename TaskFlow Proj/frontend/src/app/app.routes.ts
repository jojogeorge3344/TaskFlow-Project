import { Routes } from '@angular/router';
import { authGuard, mfaSetupGuard, permissionGuard, securitySetupGuard } from './core/auth/auth.guard';
import { Permissions } from './core/auth/permissions';

/**
 * Authenticated screens are children of the shell route, so they get the header and side menu
 * automatically. Add the screen's menu entry to core/layout/nav-items.ts.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent)
  },
  {
    // Forgot password from the sign-in page: authenticator code, then a new password.
    path: 'account-recovery',
    loadComponent: () =>
      import('./features/auth/account-recovery/account-recovery.component').then((m) => m.AccountRecoveryComponent)
  },
  {
    // First stop for anyone who has not set up two-step verification: their details, then the authenticator app.
    path: 'security-setup',
    canActivate: [authGuard, securitySetupGuard],
    loadComponent: () =>
      import('./features/onboarding/security-setup.component').then((m) => m.SecuritySetupComponent)
  },
  {
    path: 'legal/copyright',
    loadComponent: () => import('./features/legal/legal-page.component').then((m) => m.LegalPageComponent),
    data: { documentId: 'copyright' }
  },
  {
    path: 'legal/terms',
    loadComponent: () => import('./features/legal/legal-page.component').then((m) => m.LegalPageComponent),
    data: { documentId: 'terms' }
  },
  {
    path: 'legal/privacy',
    loadComponent: () => import('./features/legal/legal-page.component').then((m) => m.LegalPageComponent),
    data: { documentId: 'privacy' }
  },
  {
    path: 'legal/disclaimer',
    loadComponent: () => import('./features/legal/legal-page.component').then((m) => m.LegalPageComponent),
    data: { documentId: 'disclaimer' }
  },
  {
    path: 'legal/licenses',
    loadComponent: () => import('./features/legal/legal-page.component').then((m) => m.LegalPageComponent),
    data: { documentId: 'licenses' }
  },
  {
    path: '',
    canActivate: [authGuard, mfaSetupGuard],
    loadComponent: () => import('./core/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      {
        path: 'admin',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.DashboardAdmin] },
        loadComponent: () =>
          import('./features/dashboard/admin-dashboard.component').then((m) => m.AdminDashboardComponent)
      },
      {
        path: 'manager',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.DashboardManager] },
        loadComponent: () =>
          import('./features/dashboard/manager-dashboard.component').then((m) => m.ManagerDashboardComponent)
      },
      {
        // Every signed-in user has a personal dashboard; custom roles land here.
        path: 'me',
        loadComponent: () =>
          import('./features/dashboard/employee-dashboard.component').then((m) => m.EmployeeDashboardComponent)
      },
      {
        path: 'projects',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.ProjectsView] },
        loadComponent: () =>
          import('./features/projects/project-list.component').then((m) => m.ProjectListComponent)
      },
      {
        path: 'departments',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.DepartmentsManage] },
        loadComponent: () =>
          import('./features/departments/department-list.component').then((m) => m.DepartmentListComponent)
      },
      {
        path: 'employees',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.EmployeesManage] },
        loadComponent: () =>
          import('./features/employees/employee-list.component').then((m) => m.EmployeeListComponent)
      },
      {
        path: 'workload',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.WorkloadView] },
        loadComponent: () =>
          import('./features/workload/workload.component').then((m) => m.WorkloadComponent)
      },
      {
        path: 'reports',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.ReportsView] },
        loadComponent: () =>
          import('./features/reports/report.component').then((m) => m.ReportComponent)
      },
      {
        // Every signed-in user has a mailbox.
        path: 'mail',
        loadComponent: () => import('./features/mail/mail.component').then((m) => m.MailComponent)
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/notifications/notification-center.component').then(
            (m) => m.NotificationCenterComponent
          )
      },
      {
        path: 'settings/reminders',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.SettingsReminders] },
        loadComponent: () =>
          import('./features/settings/reminder-settings.component').then((m) => m.ReminderSettingsComponent)
      },
      {
        path: 'settings/application',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.SettingsApplication] },
        loadComponent: () =>
          import('./features/settings/application-settings.component').then((m) => m.ApplicationSettingsComponent)
      },
      {
        path: 'settings/tfa',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.SettingsAi] },
        loadComponent: () => import('./features/settings/tfa-settings.component').then((m) => m.TfaSettingsComponent)
      },
      {
        path: 'settings/permissions',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.RolesManage] },
        loadComponent: () =>
          import('./features/settings/permissions-management.component').then((m) => m.PermissionsManagementComponent)
      },
      {
        // Every signed-in user maintains their own profile (Screen 19).
        path: 'profile',
        loadComponent: () => import('./features/profile/profile.component').then((m) => m.ProfileComponent)
      },
      {
        path: 'board',
        loadComponent: () => import('./features/tasks/kanban.component').then((m) => m.KanbanComponent)
      },
      {
        path: 'tasks',
        pathMatch: 'full',
        loadComponent: () => import('./features/tasks/task-list.component').then((m) => m.TaskListComponent)
      },
      {
        // Before 'tasks/:id' so "new" is not read as a task id.
        path: 'tasks/new',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.TasksCreate] },
        loadComponent: () =>
          import('./features/tasks/task-create.component').then((m) => m.TaskCreateComponent)
      },
      {
        path: 'tasks/:id',
        loadComponent: () =>
          import('./features/tasks/task-details.component').then((m) => m.TaskDetailsComponent)
      },
      {
        path: 'tasks/:id/discussion',
        loadComponent: () =>
          import('./features/tasks/task-discussion.component').then((m) => m.TaskDiscussionComponent)
      },
      {
        path: 'tasks/:id/activity',
        loadComponent: () => import('./features/tasks/activity.component').then((m) => m.ActivityComponent)
      },
      {
        path: 'tasks/:id/reassign',
        canActivate: [permissionGuard],
        data: { permissions: [Permissions.TasksReassign] },
        loadComponent: () =>
          import('./features/tasks/reassign.component').then((m) => m.TaskReassignComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
