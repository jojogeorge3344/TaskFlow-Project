import { DatePipe } from '@angular/common';
import { Component, ElementRef, Injector, OnInit, afterNextRender, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { PasswordResetRequest, User } from '../../core/models';
import { IconComponent } from '../../core/layout/icon.component';
import { PasswordResetService } from '../../core/services/password-reset.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { roleBadgeClass } from './role-badge';
import { initials } from '../../shared/ui/initials';

/** Read-only employee details shown inside the side panel, plus (for administrators) the employee's
 * password reset requests with the Set-password and Retry-notification actions. */
@Component({
  selector: 'app-employee-details-panel',
  standalone: true,
  imports: [DatePipe, IconComponent, FormsModule],
  template: `
    <div class="tf-drawer__body">
      <div class="details__hero">
        <span class="tf-avatar tf-avatar--lg" aria-hidden="true">{{ avatar() }}</span>
        <div>
          <div class="details__name">{{ employee().fullName }}</div>
          <div class="details__sub">{{ employee().email }}</div>
        </div>
      </div>

      <dl class="details__list">
        <dt>Employee code</dt>
        <dd>{{ employee().employeeCode || '—' }}</dd>
        <dt>Username</dt>
        <dd>{{ employee().username }}</dd>
        <dt>Email</dt>
        <dd>{{ employee().email }}</dd>
        <dt>Role</dt>
        <dd><span [class]="roleBadgeClass(employee().role)">{{ employee().role }}</span></dd>
        <dt>Project</dt>
        <dd>{{ employee().projectName || 'Unassigned' }}</dd>
        <dt>Department</dt>
        <dd data-field="department">{{ employee().departmentName || 'Unassigned' }}</dd>
        <dt>Manager</dt>
        <dd data-field="manager">{{ employee().managerName || 'None' }}</dd>
        @if (employee().role === 'Manager') {
          <dt>Manages projects</dt>
          <dd data-field="managed-projects">{{ managedProjects() }}</dd>
          <dt>Team</dt>
          <dd data-field="team">{{ team() }}</dd>
        }
        <dt>Status</dt>
        <dd>
          <span class="tf-badge" [class.tf-badge--active]="employee().isActive" [class.tf-badge--inactive]="!employee().isActive">
            {{ employee().isActive ? 'Active' : 'Inactive' }}
          </span>
        </dd>
        <dt>Created</dt>
        <dd>{{ employee().createdAt | date: 'mediumDate' }}</dd>
        <dt>Last login</dt>
        <dd>{{ employee().lastLoginAt ? (employee().lastLoginAt | date: 'medium') : 'Never' }}</dd>
      </dl>

      @if (canManagePasswordResets) {
        <section class="resets" data-section="password-reset-requests">
          <h3 class="resets__title">Password reset request</h3>

          @if (resetError(); as message) {
            <p class="resets__error" role="alert" data-error="password-reset">{{ message }}</p>
          }

          @if (focusNote(); as note) {
            <p class="resets__note" role="status" data-field="focus-note">{{ note }}</p>
          }

          @if (requests().length === 0) {
            <p class="resets__empty">
              {{ resetsLoaded() ? 'No password reset requests for this employee.' : 'Loading…' }}
            </p>
          }

          @for (request of requests(); track request.referenceId) {
            <article
              class="resets__item"
              [class.resets__item--focus]="request.referenceId === focusRequest()"
              [attr.data-request]="request.referenceId"
              [attr.data-focused]="request.referenceId === focusRequest() ? 'true' : null"
            >
              <div class="resets__meta">
                <span class="tf-badge" data-field="request-status">{{ request.status }}</span>
                <span data-field="request-origin">{{ request.origin === 'LoginScreen' ? 'Login Screen' : 'Profile Screen' }}</span>
                <span data-field="request-created">{{ request.createdAt | date: 'medium' }}</span>
              </div>

              @if (request.failureReason) {
                <p class="resets__error" data-field="request-failure">{{ request.failureReason }}</p>
              }

              @if (request.status === 'Pending') {
                <input
                    class="tf-input"
                    type="password"
                    autocomplete="new-password"
                    placeholder="New password (you pass it to the user)"
                    data-field="supplied-password"
                    [ngModel]="suppliedFor(request.referenceId)"
                    (ngModelChange)="setSupplied(request.referenceId, $event)"
                />
                <button
                  type="button"
                  class="tf-btn tf-btn--primary"
                  data-action="process-reset"
                  [disabled]="busy() === request.referenceId"
                  (click)="process(request)"
                >
                  Set password
                </button>
              }

              @if (request.status === 'Completed') {
                <button
                  type="button"
                  class="tf-btn tf-btn--ghost"
                  data-action="show-login-link"
                  (click)="showLoginLink(request)"
                >
                  Generate login link for the user
                </button>
                @if (loginLink(); as link) {
                  <div class="resets__link">
                    <input class="tf-input" type="text" readonly [value]="link" data-field="login-link" aria-label="Login link for the user" />
                    <button type="button" class="tf-btn tf-btn--ghost" data-action="copy-login-link" (click)="copyLoginLink(link)">Copy</button>
                    @if (linkCopied()) {
                      <span class="resets__note" role="status" data-status="link-copied">Link copied</span>
                    }
                  </div>
                  <p class="resets__note" data-field="link-note">This link works once and expires in 1 minute. Send it only when the user is ready, and generate a new one if it runs out. It does not contain the password.</p>
                }
              }

              @if (request.status === 'Failed') {
                <button
                  type="button"
                  class="tf-btn tf-btn--ghost"
                  data-action="retry-notification"
                  [disabled]="busy() === request.referenceId"
                  (click)="retry(request)"
                >
                  Retry notification
                </button>
              }
            </article>
          }
        </section>
      }
    </div>

    <div class="tf-drawer__foot">
      <button type="button" class="tf-btn tf-btn--ghost" (click)="closed.emit()">Close</button>
      <button type="button" class="tf-btn tf-btn--primary" (click)="edit.emit()">
        <tf-icon name="edit" />
        Edit
      </button>
    </div>
  `,
  styles: `
    .details__hero {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .details__name {
      font-size: 1rem;
      font-weight: 600;
    }

    .details__sub {
      color: var(--tf-muted);
      overflow-wrap: anywhere;
    }

    .details__list {
      display: grid;
      grid-template-columns: 8rem 1fr;
      gap: 0.625rem 1rem;
      margin: 0;
    }

    .details__list dt {
      color: var(--tf-muted);
      font-size: 0.8125rem;
    }

    .details__list dd {
      margin: 0;
      font-weight: 500;
      overflow-wrap: anywhere;
    }

    .resets {
      margin-top: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }

    .resets__title {
      margin: 0;
      font-size: 0.9375rem;
    }

    .resets__item {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 0.75rem;
      border: 1px solid var(--tf-border, #e2e8f0);
      border-radius: 0.5rem;
    }

    .resets__meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 0.75rem;
      align-items: center;
      color: var(--tf-muted);
      font-size: 0.8125rem;
    }

    .resets__error {
      margin: 0;
      color: #b91c1c;
      font-size: 0.8125rem;
    }

    .resets__item--focus {
      border-color: var(--tf-primary, #2563eb);
      box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.25);
    }

    .resets__note {
      margin: 0;
      font-size: 0.8125rem;
    }

    .resets__link {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }

    .resets__link input {
      flex: 1;
      min-width: 0;
    }

    .resets__empty {
      margin: 0;
      color: var(--tf-muted);
      font-size: 0.8125rem;
    }
  `
})
export class EmployeeDetailsPanelComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly passwordResetService = inject(PasswordResetService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  readonly roleBadgeClass = roleBadgeClass;

  readonly employee = input.required<User>();
  /** The request named by the administrator mail's link: highlighted, and its password field focused. */
  readonly focusRequest = input<string | null>(null);
  readonly edit = output<void>();
  readonly closed = output<void>();

  readonly avatar = computed(() => initials(this.employee().fullName));

  readonly managedProjects = computed(
    () => (this.employee().managerProjects ?? []).map((project) => project.name).join(', ') || 'None'
  );

  readonly team = computed(
    () => (this.employee().reports ?? []).map((report) => report.fullName).join(', ') || 'No one yet'
  );

  /** The Password reset requests section is for administrators only; the API checks every call again. */
  readonly canManagePasswordResets = this.authService.hasPermission(Permissions.EmployeesManage);

  /** Every request this employee has raised, newest first. Only the latest is shown: no history list. */
  private readonly allRequests = signal<PasswordResetRequest[]>([]);
  readonly requests = computed(() => this.allRequests().slice(0, 1));
  readonly resetsLoaded = signal(false);
  readonly resetError = signal<string | null>(null);
  /** Reference id of the request being processed, to disable its buttons. */
  readonly busy = signal<string | null>(null);
  /** Administrator-typed passwords per request; dropped once processed, never displayed. */
  readonly supplied = signal<Record<string, string>>({});

  /** Says why the linked request is not actionable here: handled already, or not this employee's. */
  readonly focusNote = computed(() => {
    const reference = this.focusRequest();
    if (!reference || !this.resetsLoaded()) {
      return null;
    }

    const request = this.requests().find((candidate) => candidate.referenceId === reference);
    if (!request) {
      return this.allRequests().some((candidate) => candidate.referenceId === reference)
        ? 'A newer password reset request exists for this employee; the latest one is shown.'
        : 'The request in that link does not belong to this employee.';
    }

    return request.status === 'Pending' ? null : `That request is already ${request.status.toLowerCase()}.`;
  });

  ngOnInit(): void {
    if (this.canManagePasswordResets) {
      this.loadRequests();
    }
  }

  /** The user's encrypted login link, once fetched. It holds no password (design D14). */
  readonly loginLink = signal<string | null>(null);
  readonly linkCopied = signal(false);

  /** Fetches the encrypted login link for a completed request and shows it for copying. */
  showLoginLink(request: PasswordResetRequest): void {
    this.resetError.set(null);
    this.linkCopied.set(false);
    this.passwordResetService.loginLink(request.referenceId).subscribe({
      next: (link) => this.loginLink.set(`${window.location.origin}${link.path}`),
      error: (err: unknown) => this.resetError.set(apiErrorMessage(err, 'Could not get the login link.'))
    });
  }

  copyLoginLink(link: string): void {
    navigator.clipboard?.writeText(link).then(
      () => this.linkCopied.set(true),
      () => this.resetError.set('Could not copy automatically. Select the link and copy it.')
    );
  }

  suppliedFor(referenceId: string): string {
    return this.supplied()[referenceId] ?? '';
  }

  setSupplied(referenceId: string, value: string): void {
    this.supplied.update((current) => ({ ...current, [referenceId]: value }));
  }

  process(request: PasswordResetRequest): void {
    const typed = (this.supplied()[request.referenceId] ?? '').trim();
    if (typed === '') {
      this.resetError.set('Enter the new password to set for this account.');
      return;
    }

    this.run(request, this.passwordResetService.process(request.referenceId, typed));
  }

  retry(request: PasswordResetRequest): void {
    this.run(request, this.passwordResetService.retryNotification(request.referenceId));
  }

  private run(request: PasswordResetRequest, call: ReturnType<PasswordResetService['process']>): void {
    this.resetError.set(null);
    this.busy.set(request.referenceId);
    call.subscribe({
      next: () => this.afterAction(request.referenceId, false),
      error: (err: unknown) => {
        // A refused or failed attempt may still have changed the request (e.g. Failed), so refresh the list too.
        this.resetError.set(apiErrorMessage(err, 'The password reset action failed.'));
        this.afterAction(request.referenceId, true);
      }
    });
  }

  private afterAction(referenceId: string, keepError: boolean): void {
    this.supplied.update((current) => {
      const next = { ...current };
      delete next[referenceId];
      return next;
    });
    this.busy.set(null);
    this.loadRequests(keepError);
  }

  /** Puts the cursor in the password field of the linked request once it has rendered. */
  private focusLinkedRequest(): void {
    const reference = this.focusRequest();
    if (!reference) {
      return;
    }

    afterNextRender(
      () =>
        this.host.nativeElement
          .querySelector<HTMLInputElement>(`[data-request="${reference}"] [data-field="supplied-password"]`)
          ?.focus(),
      { injector: this.injector }
    );
  }

  private loadRequests(keepError = false): void {
    this.passwordResetService.list().subscribe({
      next: (all) => {
        this.allRequests.set(
          all
            .filter((request) => request.userId === this.employee().id)
            .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        );
        this.resetsLoaded.set(true);
        this.focusLinkedRequest();
      },
      error: (err: unknown) => {
        this.resetsLoaded.set(true);
        if (!keepError) {
          this.resetError.set(apiErrorMessage(err, 'Could not load password reset requests.'));
        }
      }
    });
  }
}
