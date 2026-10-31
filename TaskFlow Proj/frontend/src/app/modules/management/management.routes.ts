import { Routes } from '@angular/router';
import { permissionGuard } from '../../shared/auth/auth.guard';
import { DepartmentScreenPermissions, EmployeeScreenPermissions, Permissions } from '../../shared/auth/permissions';

export const MANAGEMENT_ROUTES: Routes = [
  {
    path: 'projects',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.ProjectsView] },
    loadComponent: () =>
      import('./project-management/pages/project-list.component').then((m) => m.ProjectListComponent)
  },
  {
    path: 'projects/:id',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.ProjectsView] },
    loadComponent: () =>
      import('./project-management/pages/project-detail.component').then((m) => m.ProjectDetailComponent)
  },
  {
    path: 'departments',
    canActivate: [permissionGuard],
    data: { permissions: DepartmentScreenPermissions },
    loadComponent: () =>
      import('./department-management/pages/department-list.component').then((m) => m.DepartmentListComponent)
  },
  {
    path: 'employees',
    canActivate: [permissionGuard],
    data: { permissions: EmployeeScreenPermissions },
    loadComponent: () =>
      import('./employee-management/pages/employee-list.component').then((m) => m.EmployeeListComponent)
  }
];
