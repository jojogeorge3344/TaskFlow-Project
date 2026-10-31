import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { Component, DestroyRef, OnInit, inject, signal, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { MailComposeMode, MailListItem, MailSummary, MailView } from '../models/mail.model';
import { MailService } from '../services/mail.service';
import { ComposeDialogComponent } from '../components/compose-dialog.component';
import { ComposeRequest, ComposeResult } from '../components/compose-dialog.model';
import { MailListComponent } from '../components/mail-list.component';
import { MAIL_FOLDERS, MailFolderDef, folderFromParam } from '../utils/mail-format';
import { MailReaderComponent } from '../components/mail-reader.component';

/** Below this width the screen shows one pane at a time (folders, list or mail). */
export const MAIL_NARROW_QUERY = '(max-width: 767.98px)';

/** Mail (internal mail between TaskFlow users): folder rail, message list, reading pane and composer. The folder
 * and opened mail live in the URL (`/mail?folder=sent&item=42`), so back and forward work. */
@Component({
  selector: 'app-mail',
  standalone: true,
  imports: [IconComponent, MailListComponent, MailReaderComponent, ComposeDialogComponent],
  templateUrl: './mail.component.html',
  styleUrl: './mail.component.css'
})
export class MailComponent implements OnInit {
  private readonly permissionAuth = inject(AuthService);
  readonly canCompose = computed(() => {
    this.permissionAuth.currentUser();
    return this.permissionAuth.hasPermission(Permissions.MailCompose);
  });

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly mailService = inject(MailService);
  private readonly destroyRef = inject(DestroyRef);

  readonly folders = MAIL_FOLDERS;
  readonly folder = signal<MailView>('Inbox');
  readonly itemId = signal<number | null>(null);
  readonly summary = signal<MailSummary | null>(null);
  readonly refreshToken = signal(0);
  readonly compose = signal<ComposeRequest | null>(null);

  /** Narrow screens show one pane; `foldersOpen` swaps the list for the folder rail. */
  readonly narrow = signal(false);
  readonly foldersOpen = signal(false);

  ngOnInit(): void {
    this.watchViewport();
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.folder.set(folderFromParam(params.get('folder')));
      const item = Number(params.get('item'));
      this.itemId.set(Number.isInteger(item) && item > 0 ? item : null);
    });
    this.loadSummary();
  }

  /** The unread or draft count shown beside a folder. */
  count(folder: MailFolderDef): number {
    const summary = this.summary();
    if (!summary) {
      return 0;
    }
    switch (folder.view) {
      case 'Inbox':
        return summary.inboxUnread;
      case 'Starred':
        return summary.starredUnread;
      case 'Important':
        return summary.importantUnread;
      case 'Drafts':
        return summary.drafts;
      default:
        return 0;
    }
  }

  selectFolder(view: MailView): void {
    this.foldersOpen.set(false);
    void this.router.navigate([], { relativeTo: this.route, queryParams: { folder: view.toLowerCase(), item: null } });
  }

  openItem(item: MailListItem): void {
    if (item.folder === 'Drafts' && !item.isTrashed) {
      this.compose.set({ kind: 'draft', itemId: item.itemId });
      return;
    }
    void this.router.navigate([], { relativeTo: this.route, queryParams: { item: item.itemId }, queryParamsHandling: 'merge' });
  }

  closeItem(): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { item: null }, queryParamsHandling: 'merge' });
  }

  newMail(): void {
    this.compose.set({ kind: 'new' });
  }

  replyTo(event: { itemId: number; mode: MailComposeMode }): void {
    this.compose.set({ kind: 'reply', itemId: event.itemId, mode: event.mode });
  }

  onComposeClosed(result: ComposeResult): void {
    this.compose.set(null);
    if (result !== 'cancelled') {
      this.changed();
    }
  }

  /** Something changed (flags, folders, a send): reload the list and the counts. */
  changed(): void {
    this.refreshToken.update((token) => token + 1);
    this.loadSummary();
  }

  /** Which pane a narrow screen shows. */
  pane(): 'folders' | 'list' | 'reader' {
    if (this.itemId() !== null) {
      return 'reader';
    }
    return this.foldersOpen() ? 'folders' : 'list';
  }

  currentFolderLabel(): string {
    return this.folders.find((folder) => folder.view === this.folder())?.label ?? '';
  }

  private loadSummary(): void {
    this.mailService.summary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.mailService.unreadCount.set(summary.inboxUnread);
      },
      error: () => undefined
    });
  }

  private watchViewport(): void {
    if (typeof window === 'undefined' || typeof window.matchMedia !== 'function') {
      return;
    }
    const query = window.matchMedia(MAIL_NARROW_QUERY);
    this.narrow.set(query.matches);
    const listener = (event: MediaQueryListEvent) => this.narrow.set(event.matches);
    query.addEventListener('change', listener);
    this.destroyRef.onDestroy(() => query.removeEventListener('change', listener));
  }
}
