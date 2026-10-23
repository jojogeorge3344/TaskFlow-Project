import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { IconComponent } from '../../../core/layout/icon.component';
import { PasswordResetRequestDetails } from '../../../core/models';
import { roleBadgeClass } from '../../employees/role-badge';
import { apiErrorMessage } from '../../../shared/ui/api-error';
import { initials } from '../../../shared/ui/initials';
import { ToastService } from '../../../shared/ui/toast.service';

const INVALID_LINK_MESSAGE =
  'The link is not valid or has expired. Reset links are valid for 5 minutes and can be used once.';

/**
 * Landing page for the one-time link in the administrator's Password Request mail
 * (`/reset-password?token=…&userId=…`). It validates the link, shows the requesting user's details and how
 * many times they asked for a reset (history is an expandable section, collapsed by default), and lets the
 * Administrator generate the new password.
 */
@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [DatePipe, IconComponent, RouterLink],
  template: `
    <section class="tf-page rp">
      @if (loading()) {
        <div class="rp__window rp__state" aria-busy="true">
          <span class="rp__state-icon"><tf-icon name="clock" /></span>
          <strong>Checking the reset link…</strong>
        </div>
      } @else if (error()) {
        <div class="rp__window rp__state" role="alert">
          <span class="rp__state-icon rp__state-icon--danger"><tf-icon name="alert" /></span>
          <strong>This reset link cannot be used</strong>
          <p>{{ error() }}</p>
          <a class="tf-btn tf-btn--ghost" routerLink="/mail" [queryParams]="{ folder: 'passwordrequest' }">
            Back to Password Request
          </a>
        </div>
      } @else if (done()) {
        <div class="rp__window rp__state" role="status" data-state="done">
          <span class="rp__state-icon rp__state-icon--success"><tf-icon name="check" /></span>
          <strong>New password generated</strong>
          <p>
            A new password was emailed to {{ details()!.user.fullName }} and they were notified. Their old sessions
            have been ended.
          </p>
          <a class="tf-btn tf-btn--primary" routerLink="/mail" [queryParams]="{ folder: 'passwordrequest' }">
            Back to Password Request
          </a>
        </div>
      } @else {
        @if (details(); as data) {
        <article class="rp__window" aria-labelledby="rp-title">
          <header class="rp__head">
            <span class="tf-avatar tf-avatar--lg" aria-hidden="true">{{ avatar() }}</span>
            <div class="rp__who">
              <p class="rp__eyebrow">Password reset request</p>
              <h1 class="rp__name" id="rp-title">{{ data.user.fullName }}</h1>
              <p class="rp__email">{{ data.user.email }}</p>
            </div>
            <a
              class="tf-btn tf-btn--icon rp__close"
              routerLink="/mail"
              [queryParams]="{ folder: 'passwordrequest' }"
              aria-label="Close"
              title="Close"
            >
              <tf-icon name="x" />
            </a>
          </header>

          <div class="rp__body">
            <div class="rp__stats">
              <div class="rp__stat" data-stat="count">
                <span class="rp__stat-value">{{ data.requestCount }}</span>
                <span class="rp__stat-label">{{ data.requestCount === 1 ? 'Request' : 'Requests' }} so far</span>
              </div>
              <div class="rp__stat">
                <span class="rp__stat-value rp__stat-value--small">
                  {{ data.lastRequestedAt ? (data.lastRequestedAt | date: 'medium') : '—' }}
                </span>
                <span class="rp__stat-label">Last requested</span>
              </div>
              <div class="rp__stat">
                <span class="rp__stat-value rp__stat-value--small">{{ data.linkExpiresAt | date: 'shortTime' }}</span>
                <span class="rp__stat-label">This link expires</span>
              </div>
            </div>

            <h2 class="rp__section">User details</h2>
            <dl class="rp__grid">
              <div><dt>Employee code</dt><dd>{{ data.user.employeeCode || '—' }}</dd></div>
              <div><dt>Username</dt><dd>{{ data.user.username }}</dd></div>
              <div><dt>Email</dt><dd>{{ data.user.email }}</dd></div>
              <div>
                <dt>Role</dt>
                <dd><span [class]="roleBadgeClass(data.user.role)">{{ data.user.role }}</span></dd>
              </div>
              <div><dt>Project</dt><dd>{{ data.user.projectName || 'Unassigned' }}</dd></div>
              <div><dt>Department</dt><dd>{{ data.user.departmentName || 'Unassigned' }}</dd></div>
              <div><dt>Manager</dt><dd>{{ data.user.managerName || 'None' }}</dd></div>
              <div>
                <dt>Status</dt>
                <dd>
                  <span
                    class="tf-badge"
                    [class.tf-badge--active]="data.user.isActive"
                    [class.tf-badge--inactive]="!data.user.isActive"
                  >
                    {{ data.user.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </dd>
              </div>
              <div><dt>Created</dt><dd>{{ data.user.createdAt | date: 'mediumDate' }}</dd></div>
              <div>
                <dt>Last login</dt>
                <dd>{{ data.user.lastLoginAt ? (data.user.lastLoginAt | date: 'medium') : 'Never' }}</dd>
              </div>
            </dl>

            <section class="rp__history">
              <h2 class="rp__history-title">
                <button
                  type="button"
                  class="rp__toggle"
                  data-action="toggle-history"
                  [attr.aria-expanded]="historyOpen()"
                  aria-controls="rp-history"
                  (click)="historyOpen.set(!historyOpen())"
                >
                  <span>
                    Password request history
                    <span class="rp__pill">{{ data.requestCount }}</span>
                  </span>
                  <span class="rp__chevron" [class.rp__chevron--open]="historyOpen()"><tf-icon name="chevron" /></span>
                </button>
              </h2>
              @if (historyOpen()) {
                <ol class="rp__list" id="rp-history" data-region="history">
                  @for (entry of data.requests; track entry.id; let index = $index) {
                    <li class="rp__entry">
                      <span class="rp__entry-no">#{{ data.requestCount - index }}</span>
                      <span class="rp__entry-when">{{ entry.requestedAt | date: 'medium' }}</span>
                      <span class="rp__status" [attr.data-status]="entry.status">{{ entry.status }}</span>
                    </li>
                  } @empty {
                    <li class="rp__entry rp__entry--empty">No requests recorded.</li>
                  }
                </ol>
              }
            </section>
          </div>

          <footer class="rp__foot">
            <a class="tf-btn tf-btn--ghost" routerLink="/mail" [queryParams]="{ folder: 'passwordrequest' }">Close</a>
            <button
              type="button"
              class="tf-btn tf-btn--primary"
              data-action="generate-password"
              [disabled]="generating()"
              (click)="generate()"
            >
              <tf-icon name="lock" />
              {{ generating() ? 'Generating…' : 'Generate new password' }}
            </button>
          </footer>
        </article>
        }
      }
    </section>
  `,
  styles: `
    .rp {
      display: flex;
      justify-content: center;
      align-items: flex-start;
    }

    .rp__window {
      width: 100%;
      max-width: 46rem;
      background: var(--tf-surface);
      border: 1px solid var(--tf-border);
      border-radius: var(--tf-radius-lg);
      box-shadow: var(--tf-shadow-lg);
      overflow: hidden;
    }

    .rp__head {
      display: flex;
      align-items: center;
      gap: var(--tf-space-4);
      padding: var(--tf-space-5);
      background: linear-gradient(135deg, var(--tf-primary-soft), var(--tf-surface));
      border-bottom: 1px solid var(--tf-border);
    }

    .rp__who {
      min-width: 0;
      flex: 1;
    }

    .rp__eyebrow {
      margin: 0;
      font-size: 0.75rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--tf-primary);
    }

    .rp__name {
      margin: 0.125rem 0 0;
      font-size: 1.25rem;
      color: var(--tf-text);
      overflow-wrap: anywhere;
    }

    .rp__email {
      margin: 0.125rem 0 0;
      color: var(--tf-muted);
      font-size: 0.875rem;
      overflow-wrap: anywhere;
    }

    .rp__close {
      align-self: flex-start;
    }

    .rp__body {
      display: flex;
      flex-direction: column;
      gap: var(--tf-space-4);
      padding: var(--tf-space-5);
    }

    .rp__stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
      gap: var(--tf-space-3);
    }

    .rp__stat {
      display: flex;
      flex-direction: column;
      gap: 0.125rem;
      padding: var(--tf-space-3) var(--tf-space-4);
      border: 1px solid var(--tf-border);
      border-radius: var(--tf-radius-md);
      background: var(--tf-surface-alt);
    }

    .rp__stat-value {
      font-size: 1.75rem;
      font-weight: 700;
      line-height: 1.2;
      color: var(--tf-text);
    }

    .rp__stat-value--small {
      font-size: 0.9375rem;
      font-weight: 600;
      line-height: 1.8;
    }

    .rp__stat-label {
      font-size: 0.75rem;
      color: var(--tf-muted);
    }

    .rp__section {
      margin: var(--tf-space-2) 0 0;
      font-size: 0.8125rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--tf-text-secondary);
    }

    .rp__grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
      gap: var(--tf-space-4) var(--tf-space-5);
      margin: 0;
    }

    .rp__grid dt {
      font-size: 0.75rem;
      color: var(--tf-muted);
    }

    .rp__grid dd {
      margin: 0.125rem 0 0;
      color: var(--tf-text);
      font-size: 0.9375rem;
      overflow-wrap: anywhere;
    }

    .rp__history {
      border: 1px solid var(--tf-border);
      border-radius: var(--tf-radius-md);
      overflow: hidden;
    }

    .rp__history-title {
      margin: 0;
    }

    .rp__toggle {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--tf-space-3);
      padding: var(--tf-space-3) var(--tf-space-4);
      border: 0;
      background: var(--tf-surface-alt);
      color: var(--tf-text);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
      text-align: left;
    }

    .rp__toggle:hover {
      background: var(--tf-primary-soft);
    }

    .rp__toggle:focus-visible {
      outline: none;
      box-shadow: inset var(--tf-focus-ring);
    }

    .rp__pill {
      margin-left: 0.5rem;
      padding: 0.0625rem 0.5rem;
      border-radius: 999px;
      background: var(--tf-primary);
      color: #ffffff;
      font-size: 0.75rem;
    }

    .rp__chevron {
      display: inline-flex;
      color: var(--tf-muted);
      transition: transform 0.15s ease;
    }

    .rp__chevron--open {
      transform: rotate(180deg);
    }

    .rp__list {
      list-style: none;
      margin: 0;
      padding: 0;
      max-height: 16rem;
      overflow-y: auto;
    }

    .rp__entry {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--tf-space-3);
      padding: var(--tf-space-3) var(--tf-space-4);
      border-top: 1px solid var(--tf-border);
      font-size: 0.875rem;
    }

    .rp__entry--empty {
      color: var(--tf-muted);
    }

    .rp__entry-no {
      font-weight: 600;
      color: var(--tf-text-secondary);
      min-width: 2.25rem;
    }

    .rp__entry-when {
      flex: 1;
      color: var(--tf-text);
    }

    .rp__status {
      padding: 0.125rem 0.625rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
      background: var(--tf-warning-soft);
      color: var(--tf-warning);
    }

    .rp__status[data-status='Used'] {
      background: var(--tf-success-soft);
      color: var(--tf-success);
    }

    .rp__status[data-status='Expired'] {
      background: var(--tf-surface-alt);
      color: var(--tf-muted);
    }

    .rp__foot {
      display: flex;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: var(--tf-space-3);
      padding: var(--tf-space-4) var(--tf-space-5);
      border-top: 1px solid var(--tf-border);
      background: var(--tf-surface-alt);
    }

    .rp__state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--tf-space-3);
      max-width: 28rem;
      padding: 2.5rem var(--tf-space-5);
      text-align: center;
    }

    .rp__state p {
      margin: 0;
      color: var(--tf-text-secondary);
    }

    .rp__state-icon {
      display: grid;
      place-items: center;
      width: 3rem;
      height: 3rem;
      border-radius: 50%;
      background: var(--tf-primary-soft);
      color: var(--tf-primary);
    }

    .rp__state-icon--danger {
      background: var(--tf-danger-soft);
      color: var(--tf-danger);
    }

    .rp__state-icon--success {
      background: var(--tf-success-soft);
      color: var(--tf-success);
    }

    @media (max-width: 36rem) {
      .rp__head,
      .rp__body {
        padding: var(--tf-space-4);
      }

      .rp__foot {
        flex-direction: column-reverse;
        padding: var(--tf-space-4);
      }

      .rp__foot .tf-btn {
        width: 100%;
        justify-content: center;
      }
    }
  `
})
export class ResetPasswordComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly roleBadgeClass = roleBadgeClass;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly details = signal<PasswordResetRequestDetails | null>(null);
  /** The request history starts minimised. */
  readonly historyOpen = signal(false);
  readonly generating = signal(false);
  readonly done = signal(false);

  readonly avatar = computed(() => initials(this.details()?.user.fullName));

  private token = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token')?.trim() ?? '';

    if (!this.token) {
      this.loading.set(false);
      this.error.set(INVALID_LINK_MESSAGE);
      return;
    }

    this.authService.validateResetToken(this.token).subscribe({
      next: (details) => {
        this.details.set(details);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(INVALID_LINK_MESSAGE);
        this.loading.set(false);
      }
    });
  }

  /** Generates the new password. It is emailed to the user and never shown here; the link is single-use. */
  generate(): void {
    if (this.generating()) {
      return;
    }

    this.generating.set(true);
    this.authService.generateNewPassword(this.token).subscribe({
      next: () => {
        this.generating.set(false);
        this.done.set(true);
        this.toast.success('A new password was generated and emailed to the user.');
      },
      error: (err) => {
        this.generating.set(false);
        this.toast.error(apiErrorMessage(err, 'Could not generate a new password. The link may have expired.'));
      }
    });
  }
}
