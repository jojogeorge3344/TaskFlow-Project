import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { UserRole } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';

export interface DashboardNavItem {
  label: string;
  /** Route to navigate to. When omitted the item is rendered as a disabled placeholder. */
  route?: string;
}

export const ROLE_NAVIGATION: Record<UserRole, DashboardNavItem[]> = {
  Administrator: [
    { label: 'Dashboard', route: '/admin' },
    { label: 'Employee Management', route: '/employees' },
    { label: 'Department Management', route: '/departments' },
    { label: 'Task Management' }
  ],
  Manager: [
    { label: 'Dashboard', route: '/manager' },
    { label: 'Task Management' }
  ],
  Employee: [
    { label: 'Dashboard', route: '/me' },
    { label: 'My Tasks' }
  ]
};

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './dashboard-layout.component.html',
  styleUrl: './dashboard-layout.component.css'
})
export class DashboardLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;
  readonly sidebarOpen = signal(false);

  readonly navItems = computed<DashboardNavItem[]>(() => {
    const role = this.user()?.role;
    return role ? ROLE_NAVIGATION[role] : [];
  });

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  logout(): void {
    this.authService.logout().subscribe(() => {
      void this.router.navigateByUrl('/login');
    });
  }
}
