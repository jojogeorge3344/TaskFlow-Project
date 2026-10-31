import { Routes } from '@angular/router';

export const PROFILE_ROUTES: Routes = [
  {
    // Every signed-in user maintains their own profile (Screen 19).
    path: 'profile',
    loadComponent: () => import('./pages/profile.component').then((m) => m.ProfileComponent)
  },
  {
    // Opened from the Security tab on Profile; asks for an authenticator code when the change is submitted.
    path: 'profile/change-password',
    loadComponent: () => import('./pages/change-password.component').then((m) => m.ChangePasswordComponent)
  }
];
