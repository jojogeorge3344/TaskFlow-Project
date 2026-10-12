import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guard';

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
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./core/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      {
        path: 'admin',
        canActivate: [roleGuard],
        data: { roles: ['Administrator'] },
        loadComponent: () =>
          import('./features/dashboard/admin-dashboard.component').then((m) => m.AdminDashboardComponent)
      },
      {
        path: 'manager',
        canActivate: [roleGuard],
        data: { roles: ['Manager'] },
        loadComponent: () =>
          import('./features/dashboard/manager-dashboard.component').then((m) => m.ManagerDashboardComponent)
      },
      {
        path: 'me',
        canActivate: [roleGuard],
        data: { roles: ['Employee'] },
        loadComponent: () =>
          import('./features/dashboard/employee-dashboard.component').then((m) => m.EmployeeDashboardComponent)
      },
      {
        path: 'departments',
        canActivate: [roleGuard],
        data: { roles: ['Administrator'] },
        loadComponent: () =>
          import('./features/departments/department-list.component').then((m) => m.DepartmentListComponent)
      },
      {
        path: 'employees',
        canActivate: [roleGuard],
        data: { roles: ['Administrator'] },
        loadComponent: () =>
          import('./features/employees/employee-list.component').then((m) => m.EmployeeListComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
