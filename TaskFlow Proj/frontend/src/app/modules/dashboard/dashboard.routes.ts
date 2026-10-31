import { Routes } from '@angular/router';
import { permissionGuard } from '../../shared/auth/auth.guard';
import { Permissions } from '../../shared/auth/permissions';

export const DASHBOARD_ROUTES: Routes = [
  {
    path: 'admin',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.DashboardViewOrganization] },
    loadComponent: () => import('./pages/admin-dashboard.component').then((m) => m.AdminDashboardComponent)
  },
  {
    path: 'manager',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.DashboardViewTeam] },
    loadComponent: () => import('./pages/manager-dashboard.component').then((m) => m.ManagerDashboardComponent)
  },
  {
    // Every signed-in user has a personal dashboard; custom roles land here.
    path: 'me',
    loadComponent: () => import('./pages/employee-dashboard.component').then((m) => m.EmployeeDashboardComponent)
  }
];
