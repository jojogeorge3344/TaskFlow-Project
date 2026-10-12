import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'admin',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Administrator'] },
    loadComponent: () =>
      import('./features/dashboard/admin-dashboard.component').then((m) => m.AdminDashboardComponent)
  },
  {
    path: 'manager',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Manager'] },
    loadComponent: () =>
      import('./features/dashboard/manager-dashboard.component').then((m) => m.ManagerDashboardComponent)
  },
  {
    path: 'me',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Employee'] },
    loadComponent: () =>
      import('./features/dashboard/employee-dashboard.component').then((m) => m.EmployeeDashboardComponent)
  },
  {
    path: 'departments',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Administrator'] },
    loadComponent: () => import('./features/departments/department-list.component').then((m) => m.DepartmentListComponent)
  },
  {
    path: 'employees',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Administrator'] },
    loadComponent: () => import('./features/employees/employee-list.component').then((m) => m.EmployeeListComponent)
  },
  { path: '**', redirectTo: 'login' }
];
