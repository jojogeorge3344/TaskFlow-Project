import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { Component, effect, inject, input, output, signal, untracked, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { MailAttachment, MailComposeMode, MailItemDetail, MailMessage } from '../../core/models';
import { MailService } from '../../core/services/mail.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { linkify, sameOriginPath } from '../../shared/ui/linkify';
import { ToastService } from '../../shared/ui/toast.service';
import { displayName, formatSize, fullDate, saveBlob } from './mail-format';

/** The reading pane: the opened mail's conversation, attachments and actions. */
@Component({
  selector: 'app-mail-reader',
  standalone: true,
  imports: [IconComponent, RouterLink],
  templateUrl: './mail-reader.component.html',
  styleUrl: './mail-reader.component.css'
})
export class MailReaderComponent {
  private readonly permissionAuth = inject(AuthService);
  readonly canCompose = computed(() => {
    this.permissionAuth.currentUser();
    return this.permissionAuth.hasPermission(Permissions.MailCompose);
  });
  readonly canOrganize = computed(() => {
    this.permissionAuth.currentUser();
    return this.permissionAuth.hasPermission(Permissions.MailOrganize);
  });

  private readonly mailService = inject(MailService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly itemId = input.required<number>();

  readonly back = output<void>();
  readonly reply = output<{ itemId: number; mode: MailComposeMode }>();
  /** Flags or folders changed; the list and counts should reload. */
  readonly changed = output<void>();
  /** The item left the current folder (trashed, restored or deleted). */
  readonly closed = output<void>();

  readonly detail = signal<MailItemDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly formatSize = formatSize;
  readonly fullDate = fullDate;
  readonly displayName = displayName;
  readonly linkify = linkify;

  /** Whether a link points at this application, so it can open in the same tab. */
  isInApp(url: string): boolean {
    return sameOriginPath(url, window.location.origin) !== null;
  }

  /** A plain click on a link to this application navigates inside the app (no reload, the session stays);
   * anything else - other sites, or a modified click such as ctrl-click - keeps the browser's own behaviour. */
  openLink(event: MouseEvent, url: string): void {
    const path = sameOriginPath(url, window.location.origin);
    if (path === null || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
      return;
    }

    event.preventDefault();
    void this.router.navigateByUrl(path);
  }

  constructor() {
    effect(() => {
      const itemId = this.itemId();
      untracked(() => this.load(itemId));
    });
  }

  load(itemId: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.mailService.open(itemId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.loading.set(false);
        // Opening marks it read on the server.
        this.mailService.refreshUnreadCount();
        this.changed.emit();
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not open this mail.'));
        this.loading.set(false);
      }
    });
  }

  recipients(message: MailMessage): string {
    return message.to.map((person) => person.name).join(', ');
  }

  canReply(): boolean {
    const detail = this.detail();
    return !!detail && !detail.message.isSystem && !detail.isTrashed;
  }

  startReply(mode: MailComposeMode): void {
    this.reply.emit({ itemId: this.itemId(), mode });
  }

  toggleStar(): void {
    this.setFlag({ isStarred: !this.detail()?.isStarred }, 'isStarred');
  }

  toggleImportant(): void {
    this.setFlag({ isImportant: !this.detail()?.isImportant }, 'isImportant');
  }

  markUnread(): void {
    this.mailService.setFlags(this.itemId(), { isRead: false }).subscribe({
      next: () => {
        this.mailService.refreshUnreadCount();
        this.changed.emit();
        this.back.emit();
      },
      error: (err: unknown) => this.toast.error(apiErrorMessage(err, 'Could not mark as unread.'))
    });
  }

  trash(): void {
    this.act(this.mailService.trash(this.itemId()), 'Moved to Trash');
  }

  restore(): void {
    this.act(this.mailService.restore(this.itemId()), 'Restored');
  }

  deleteForever(): void {
    this.act(this.mailService.deleteForever(this.itemId()), 'Deleted forever');
  }

  download(attachment: MailAttachment): void {
    this.mailService.download(attachment.id).subscribe({
      next: (blob) => saveBlob(blob, attachment.fileName),
      error: (err: unknown) => this.toast.error(apiErrorMessage(err, 'Could not download the file.'))
    });
  }

  private setFlag(flags: { isStarred?: boolean; isImportant?: boolean }, key: 'isStarred' | 'isImportant'): void {
    const detail = this.detail();
    if (!detail) {
      return;
    }
    this.detail.set({ ...detail, [key]: flags[key] });
    this.mailService.setFlags(this.itemId(), flags).subscribe({
      next: () => this.changed.emit(),
      error: (err: unknown) => {
        this.detail.set(detail);
        this.toast.error(apiErrorMessage(err, 'Could not update the mail.'));
      }
    });
  }

  private act(request: ReturnType<MailService['trash']>, done: string): void {
    request.subscribe({
      next: () => {
        this.toast.success(done);
        this.mailService.refreshUnreadCount();
        this.changed.emit();
        this.closed.emit();
      },
      error: (err: unknown) => this.toast.error(apiErrorMessage(err, 'Could not update the mail.'))
    });
  }
}
