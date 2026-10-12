import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { ManagerDashboardStats } from '../../core/models';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardLayoutComponent } from './dashboard-layout.component';

@Component({
  selector: 'app-manager-dashboard',
  standalone: true,
  imports: [DashboardLayoutComponent],
  templateUrl: './manager-dashboard.component.html'
})
export class ManagerDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly authService = inject(AuthService);

  readonly user = this.authService.currentUser;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly stats = signal<ManagerDashboardStats | null>(null);

  readonly completionRate = computed(() => {
    const data = this.stats();
    if (!data || data.totalTasks === 0) {
      return 0;
    }
    return Math.round((data.completedTasks / data.totalTasks) * 100);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.dashboardService.getManagerStats().subscribe({
      next: (stats) => {
        this.stats.set(stats);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load dashboard statistics. Please try again.');
        this.loading.set(false);
      }
    });
  }
}
