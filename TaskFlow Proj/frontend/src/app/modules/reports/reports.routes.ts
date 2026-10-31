import { Routes } from '@angular/router';
import { permissionGuard } from '../../shared/auth/auth.guard';
import { Permissions } from '../../shared/auth/permissions';

export const REPORTS_ROUTES: Routes = [
  {
    path: 'reports',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.ReportsView] },
    loadComponent: () => import('./pages/report.component').then((m) => m.ReportComponent)
  }
];
