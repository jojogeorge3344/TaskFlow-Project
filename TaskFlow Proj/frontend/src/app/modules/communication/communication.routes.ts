import { Routes } from '@angular/router';
import { permissionGuard } from '../../shared/auth/auth.guard';
import { Permissions } from '../../shared/auth/permissions';

export const COMMUNICATION_ROUTES: Routes = [
  {
    path: 'mail',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.MailRead] },
    loadComponent: () => import('./mail/pages/mail.component').then((m) => m.MailComponent)
  },
  {
    path: 'notifications',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.NotificationsView] },
    loadComponent: () =>
      import('./notification/pages/notification-center.component').then((m) => m.NotificationCenterComponent)
  }
];
