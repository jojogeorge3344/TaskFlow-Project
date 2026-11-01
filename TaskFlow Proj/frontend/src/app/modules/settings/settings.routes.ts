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
    // Also needs settings.ai.test: the guard passes on any one listed key, so the page checks the second itself.
    path: 'settings/tfa/models/new',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.SettingsAiEdit] },
    loadComponent: () => import('./tfa-settings/pages/add-ai-model.component').then((m) => m.AddAiModelComponent)
  },
  {
    // Edit mode of the Add AI model page; like adding, it also needs settings.ai.test, which the page checks.
    path: 'settings/tfa/models/:providerId/:name/edit',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.SettingsAiEdit] },
    loadComponent: () => import('./tfa-settings/pages/add-ai-model.component').then((m) => m.AddAiModelComponent)
  },
  {
    path: 'settings/permissions',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.RolesManage] },
    loadComponent: () =>
      import('./role-configuration/pages/permissions-management.component').then((m) => m.PermissionsManagementComponent)
  }
];
