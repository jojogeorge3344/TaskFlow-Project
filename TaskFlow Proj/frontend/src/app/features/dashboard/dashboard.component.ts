import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  template: `
    <main class="dashboard">
      <div class="dashboard__card">
        <h1 class="dashboard__title">TaskFlow</h1>
        @if (user(); as currentUser) {
          <p class="login__welcome">Welcome, {{ currentUser.fullName }}</p>
          <p class="dashboard__role">Signed in as {{ currentUser.role }}</p>
        }
        <button type="button" class="dashboard__logout" (click)="logout()">Sign out</button>
      </div>
    </main>
  `,
  styles: [
    `
      .dashboard {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        padding: 1.5rem;
        background: #0f172a;
      }

      .dashboard__card {
        width: 100%;
        max-width: 32rem;
        padding: 2.5rem;
        border-radius: 1rem;
        background: #ffffff;
        text-align: center;
      }

      .dashboard__role {
        color: #64748b;
      }

      .dashboard__logout {
        margin-top: 1rem;
        padding: 0.625rem 1.25rem;
        border: none;
        border-radius: 0.5rem;
        background: #2563eb;
        color: #ffffff;
        font-weight: 600;
        cursor: pointer;
      }
    `
  ]
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly user = this.authService.currentUser;

  logout(): void {
    this.authService.logout().subscribe(() => {
      void this.router.navigateByUrl('/login');
    });
  }
}
