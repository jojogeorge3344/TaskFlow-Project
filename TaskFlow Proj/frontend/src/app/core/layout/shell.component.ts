import { Component, DestroyRef, ElementRef, HostListener, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { EMPTY, catchError, exhaustMap, filter, fromEvent, interval, merge } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificationService } from '../services/notification.service';
import { ToastHostComponent } from '../../shared/ui/toast-host.component';
import { initials } from '../../shared/ui/initials';
import { IconComponent } from './icon.component';
import { LayoutService } from './layout.service';
import { NavItem, ROLE_NAVIGATION } from './nav-items';

export const SESSION_CHECK_INTERVAL_MS = 60_000;

/** Authenticated application frame: header bar, role-based side menu, account area and routed content. */
@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, IconComponent, ToastHostComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css'
})
export class ShellComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notificationService = inject(NotificationService);
  private readonly toggleButton = viewChild.required<ElementRef<HTMLButtonElement>>('toggleButton');

  readonly layout = inject(LayoutService);
  readonly user = this.authService.currentUser;
  readonly userInitials = computed(() => initials(this.user()?.fullName));

  /** Unread count the header bell shows as a dot (design D6/D7). */
  readonly unreadCount = this.notificationService.unreadCount;

  readonly navItems = computed<NavItem[]>(() => {
    const role = this.user()?.role;
    return role ? ROLE_NAVIGATION[role] : [];
  });

  private readonly currentUrl = signal(this.router.url);

  /** Label of the nav item matching the current route, shown in the header bar. */
  readonly pageTitle = computed(() => {
    const path = this.currentUrl().split(/[?#]/)[0];
    return this.navItems().find((item) => item.route && path.startsWith(item.route))?.label ?? '';
  });

  readonly railCollapsed = computed(() => this.layout.isWide() && this.layout.collapsed());
  readonly drawerOpen = computed(() => !this.layout.isWide() && this.layout.mobileOpen());

  readonly toggleIcon = computed(() => {
    if (!this.layout.isWide()) {
      return 'menu';
    }
    return this.layout.collapsed() ? 'panel-open' : 'panel-close';
  });

  constructor() {
    const destroyRef = inject(DestroyRef);

    // Show the badge as soon as the shell mounts, then keep it fresh.
    this.notificationService.refreshUnreadCount();

    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(destroyRef)
      )
      .subscribe((event) => {
        this.currentUrl.set(event.urlAfterRedirects);
        this.layout.closeMobile();
        this.notificationService.refreshUnreadCount();
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
      .subscribe(() => this.notificationService.refreshUnreadCount());
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
