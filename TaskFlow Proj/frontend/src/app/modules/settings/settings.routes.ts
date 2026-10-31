import { Routes } from '@angular/router';
import { permissionGuard } from '../../shared/auth/auth.guard';
import { Permissions } from '../../shared/auth/permissions';

export const SETTINGS_ROUTES: Routes = [
  {
    path: 'settings/reminders',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.SettingsReminders] },
    loadComponent: () =>
      import('./reminder-settings/pages/reminder-settings.component').then((m) => m.ReminderSettingsComponent)
  },
  {
    path: 'settings/application',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.SettingsApplication] },
    loadComponent: () =>
      import('./application-settings/pages/application-settings.component').then((m) => m.ApplicationSettingsComponent)
  },
  {
    path: 'settings/tfa',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.SettingsAiView] },
    loadComponent: () => import('./tfa-settings/pages/tfa-settings.component').then((m) => m.TfaSettingsComponent)
  },
  {
    path: 'settings/permissions',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.RolesManage] },
    loadComponent: () =>
      import('./role-configuration/pages/permissions-management.component').then((m) => m.PermissionsManagementComponent)
  }
];
