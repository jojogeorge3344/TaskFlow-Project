import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { EmployeeDashboardStats } from '../../core/models';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardLayoutComponent } from './dashboard-layout.component';

@Component({
  selector: 'app-employee-dashboard',
  standalone: true,
  imports: [DashboardLayoutComponent],
  templateUrl: './employee-dashboard.component.html'
})
export class EmployeeDashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly authService = inject(AuthService);

  readonly user = this.authService.currentUser;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly stats = signal<EmployeeDashboardStats | null>(null);

  readonly completionRate = computed(() => {
    const data = this.stats();
    if (!data || data.assignedTasks === 0) {
      return 0;
    }
    return Math.round((data.completedTasks / data.assignedTasks) * 100);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.dashboardService.getEmployeeStats().subscribe({
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
