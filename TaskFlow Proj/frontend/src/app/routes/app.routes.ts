import { Routes } from '@angular/router';
import { authGuard, mfaSetupGuard } from '../shared/auth/auth.guard';

/**
 * The root route table. Each module keeps its own routes in `modules/<module>/<module>.routes.ts`, loaded here with
 * `loadChildren`. Authenticated screens are children of the shell route, so they get the header and side menu
 * automatically. Add a screen's menu entry to layouts/nav-items.ts.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '', loadChildren: () => import('../modules/auth/auth.routes').then((m) => m.AUTH_ROUTES) },
  { path: '', loadChildren: () => import('../modules/legal/legal.routes').then((m) => m.LEGAL_ROUTES) },
  {
    path: '',
    canActivate: [authGuard, mfaSetupGuard],
    loadComponent: () => import('../layouts/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', loadChildren: () => import('../modules/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES) },
      { path: '', loadChildren: () => import('../modules/management/management.routes').then((m) => m.MANAGEMENT_ROUTES) },
      { path: '', loadChildren: () => import('../modules/monitoring/monitoring.routes').then((m) => m.MONITORING_ROUTES) },
      { path: '', loadChildren: () => import('../modules/reports/reports.routes').then((m) => m.REPORTS_ROUTES) },
      {
        path: '',
        loadChildren: () => import('../modules/communication/communication.routes').then((m) => m.COMMUNICATION_ROUTES)
      },
      { path: '', loadChildren: () => import('../modules/settings/settings.routes').then((m) => m.SETTINGS_ROUTES) },
      { path: '', loadChildren: () => import('../modules/profile/profile.routes').then((m) => m.PROFILE_ROUTES) }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
