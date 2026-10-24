import { Component, DestroyRef, ElementRef, HostListener, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { EMPTY, catchError, exhaustMap, filter, fromEvent, interval, merge } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Permissions } from '../auth/permissions';
import { MailService } from '../services/mail.service';
import { NotificationService } from '../services/notification.service';
import { ApplicationSettingsService } from '../services/application-settings.service';
import { ToastHostComponent } from '../../shared/ui/toast-host.component';
import { initials } from '../../shared/ui/initials';
import { IconComponent } from './icon.component';
import { TfaPanelComponent } from './tfa-panel.component';
import { LayoutService } from './layout.service';
import { NavItem, buildNavigation } from './nav-items';
import { LegalFooterComponent } from '../../features/legal/legal-footer.component';

export const SESSION_CHECK_INTERVAL_MS = 60_000;

/** Authenticated application frame: header bar, role-based side menu, account area and routed content. */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconComponent, ToastHostComponent, TfaPanelComponent, LegalFooterComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css'
})
export class ShellComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificationService = inject(NotificationService);
  private readonly mailService = inject(MailService);
  private readonly applicationSettings = inject(ApplicationSettingsService);
  private readonly toggleButton = viewChild.required<ElementRef<HTMLButtonElement>>('toggleButton');

  readonly layout = inject(LayoutService);
  readonly user = this.authService.currentUser;
  readonly canSeeMail = computed(() => this.user()?.permissions.includes(Permissions.MailRead) ?? false);
  readonly canSeeNotifications = computed(() => this.user()?.permissions.includes(Permissions.NotificationsView) ?? false);
  readonly canUseAssistant = computed(() => this.user()?.permissions.includes(Permissions.AssistantChat) ?? false);

  readonly userInitials = computed(() => initials(this.user()?.fullName));

  /** The configured application name (Screen 20), kept by the settings service so a save
   * refreshes the brand immediately; falls back to "TaskFlow" if the load fails. */
  readonly appName = this.applicationSettings.appName;

  /** Unread count the header bell shows as a dot (design D6/D7). */
  readonly unreadCount = this.notificationService.unreadCount;

  /** Unread Inbox mails, shown on the Mail menu entry. */
  readonly mailUnreadCount = this.mailService.unreadCount;

  readonly navItems = computed<NavItem[]>(() => buildNavigation(this.user()));

  private readonly currentUrl = signal(this.router.url);

  /** User state only: groups start collapsed and expand on navigation or click. */
  private readonly groupState = signal<Record<string, boolean>>({});

  /** Label of the nav item matching the current route, shown in the header bar. */
  readonly pageTitle = computed(() => {
    const path = this.currentUrl().split(/[?#]/)[0];
    for (const item of this.navItems()) {
      if (item.route && path.startsWith(item.route)) {
        return item.label;
      }
      const activeChild = item.children?.find((child) => child.route && path.startsWith(child.route));
      if (activeChild) {
        return activeChild.label;
      }
    }
    return '';
  });

  toggleGroup(label: string): void {
    this.groupState.set({ ...this.groupState(), [label]: !(this.groupState()[label] ?? false) });
  }

  /** Parent modules start minimized; a parent collapses when clicked even on the active route. */
  isGroupOpen(item: NavItem): boolean {
    return this.groupState()[item.label] ?? false;
  }

  /** Navigating into a sub-module expands its parent once; later manual toggles win. */
  private expandGroupContaining(path: string): void {
    const active = this.navItems().find((item) =>
      item.children?.some((child) => child.route && path.startsWith(child.route))
    );
    if (!active) {
      return;
    }
    this.groupState.set({ ...this.groupState(), [active.label]: true });
  }

  groupDomId(item: NavItem): string {
    return `nav-group-${item.label.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
  }

  readonly railCollapsed = computed(() => this.layout.isWide() && this.layout.collapsed());
  readonly drawerOpen = computed(() => !this.layout.isWide() && this.layout.mobileOpen());

  readonly toggleIcon = computed(() => {
    if (!this.layout.isWide()) {
      return 'menu';
    }
    return this.layout.collapsed() ? 'panel-open' : 'panel-close';
  });

  /** Counts only for what the role may open; a role without Mail or Notifications never asks for them. */
  private refreshBadges(): void {
    if (this.canSeeNotifications()) {
      this.notificationService.refreshUnreadCount();
    }
    if (this.canSeeMail()) {
      this.mailService.refreshUnreadCount();
    }
  }

  constructor() {
    const destroyRef = inject(DestroyRef);

    // Show the badge as soon as the shell mounts, then keep it fresh.
    this.refreshBadges();

    // Load the brand name into the settings service's cached signal; on failure it stays
    // "TaskFlow", so a slow or broken settings endpoint never blocks the shell.
    this.applicationSettings.get().subscribe({ error: () => undefined });

    // Groups are minimized on login; only the parent of the landing route starts open.
    this.expandGroupContaining(this.router.url.split(/[?#]/)[0]);

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((event) => {
        this.currentUrl.set(event.urlAfterRedirects);
        this.expandGroupContaining(event.urlAfterRedirects.split(/[?#]/)[0]);
        this.layout.closeMobile();
        this.refreshBadges();
      });

    // Re-check the session periodically and whenever the tab becomes visible, so a user whose
    // username or password was changed elsewhere is signed out promptly (the interceptor handles the 401).
    merge(
      interval(SESSION_CHECK_INTERVAL_MS),
      fromEvent(document, 'visibilitychange').pipe(filter(() => document.visibilityState === 'visible'))
    )
      .pipe(
        exhaustMap(() => this.authService.checkSession().pipe(catchError(() => EMPTY))),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe();

    // Refresh the unread badge on the same cadence, so a notification received while the tab is
    // open is reflected without a fast new timer.
    merge(
      interval(SESSION_CHECK_INTERVAL_MS),
      fromEvent(document, 'visibilitychange').pipe(filter(() => document.visibilityState === 'visible'))
    )
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => {
        this.refreshBadges();
      });
  }

  toggleMenu(): void {
    this.layout.toggle();
  }

  closeDrawer(): void {
    this.layout.closeMobile();
    this.toggleButton().nativeElement.focus();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.drawerOpen()) {
      this.closeDrawer();
    }
  }

  logout(): void {
    this.authService.logout().subscribe(() => {
      void this.router.navigateByUrl('/login');
    });
  }
}
