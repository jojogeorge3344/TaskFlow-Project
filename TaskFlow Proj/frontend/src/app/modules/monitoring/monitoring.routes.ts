import { Routes } from '@angular/router';
import { permissionGuard } from '../../shared/auth/auth.guard';
import { Permissions } from '../../shared/auth/permissions';

export const MONITORING_ROUTES: Routes = [
  {
    path: 'workload',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.WorkloadView] },
    loadComponent: () => import('./employee-workload/pages/workload.component').then((m) => m.WorkloadComponent)
  },
  {
    path: 'board',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.BoardView] },
    loadComponent: () => import('./board/pages/kanban.component').then((m) => m.KanbanComponent)
  },
  {
    path: 'tasks',
    pathMatch: 'full',
    loadComponent: () => import('./task-management/pages/task-list.component').then((m) => m.TaskListComponent)
  },
  {
    // Before 'tasks/:id' so "new" is not read as a task id.
    path: 'tasks/new',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.TasksCreate] },
    loadComponent: () => import('./task-management/pages/task-create.component').then((m) => m.TaskCreateComponent)
  },
  {
    path: 'tasks/:id',
    loadComponent: () => import('./task-management/pages/task-details.component').then((m) => m.TaskDetailsComponent)
  },
  {
    path: 'tasks/:id/discussion',
    loadComponent: () =>
      import('./task-management/discussion/pages/task-discussion.component').then((m) => m.TaskDiscussionComponent)
  },
  {
    path: 'tasks/:id/activity',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.TasksActivityView] },
    loadComponent: () =>
      import('./task-management/activity/pages/activity.component').then((m) => m.ActivityComponent)
  },
  {
    path: 'tasks/:id/reassign',
    canActivate: [permissionGuard],
    data: { permissions: [Permissions.TasksReassign] },
    loadComponent: () => import('./task-management/pages/reassign.component').then((m) => m.TaskReassignComponent)
  }
];
