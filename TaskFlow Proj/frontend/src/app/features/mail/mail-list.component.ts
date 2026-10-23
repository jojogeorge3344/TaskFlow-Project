import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { Component, effect, inject, input, output, signal, untracked, computed } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { IconComponent } from '../../core/layout/icon.component';
import { MailListItem, MailView } from '../../core/models';
import { MailService } from '../../core/services/mail.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { MAIL_FOLDERS, listDate } from './mail-format';

const PAGE_SIZE = 25;

/** One folder's mails: search, paging, star toggles and empty states. */
@Component({
  selector: 'app-mail-list',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  templateUrl: './mail-list.component.html',
  styleUrl: './mail-list.component.css'
})
export class MailListComponent {
  private readonly permissionAuth = inject(AuthService);
  readonly canOrganize = computed(() => {
    this.permissionAuth.currentUser();
    return this.permissionAuth.hasPermission(Permissions.MailOrganize);
  });

  private readonly mailService = inject(MailService);

  readonly folder = input.required<MailView>();
  readonly selectedItemId = input<number | null>(null);
  /** Bumped by the parent whenever mail changed elsewhere, to reload the page. */
  readonly refreshToken = input(0);

  readonly opened = output<MailListItem>();
  readonly changed = output<void>();

  readonly search = new FormControl('', { nonNullable: true });
  readonly items = signal<MailListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly pageSize = PAGE_SIZE;

  private readonly searchTerm = signal('');

  constructor() {
    this.search.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((term) => {
        this.page.set(1);
        this.searchTerm.set(term.trim());
      });

    // A new folder starts on page one with an empty search.
    effect(() => {
      this.folder();
      untracked(() => {
        this.page.set(1);
        this.search.setValue('', { emitEvent: false });
        this.searchTerm.set('');
      });
    });

    effect(() => {
      const folder = this.folder();
      const search = this.searchTerm();
      const page = this.page();
      this.refreshToken();
      untracked(() => this.load(folder, search, page));
    });
  }

  folderLabel(): string {
    return MAIL_FOLDERS.find((folder) => folder.view === this.folder())?.label ?? '';
  }

  pageCount(): number {
    return Math.max(1, Math.ceil(this.total() / PAGE_SIZE));
  }

  /** Who the row is about: the sender, or the recipients in Sent and Drafts. */
  who(item: MailListItem): string {
    if (item.folder === 'Drafts') {
      return item.draftTo ? `To: ${item.draftTo}` : 'Draft';
    }
    if (item.folder === 'Sent') {
      return `To: ${item.to.map((person) => person.name).join(', ')}`;
    }
    return item.from.name;
  }

  date(item: MailListItem): string {
    return listDate(item.date);
  }

  open(item: MailListItem): void {
    if (!item.isRead && item.folder !== 'Drafts') {
      this.items.update((items) => items.map((row) => (row.itemId === item.itemId ? { ...row, isRead: true } : row)));
    }
    this.opened.emit(item);
  }

  toggleStar(item: MailListItem, event: Event): void {
    event.stopPropagation();
    const isStarred = !item.isStarred;
    this.items.update((items) => items.map((row) => (row.itemId === item.itemId ? { ...row, isStarred } : row)));
    this.mailService.setFlags(item.itemId, { isStarred }).subscribe({
      next: () => this.changed.emit(),
      error: () => this.items.update((items) => items.map((row) => (row.itemId === item.itemId ? { ...row, isStarred: !isStarred } : row)))
    });
  }

  previous(): void {
    this.page.update((page) => Math.max(1, page - 1));
  }

  next(): void {
    this.page.update((page) => Math.min(this.pageCount(), page + 1));
  }

  reload(): void {
    this.load(this.folder(), this.searchTerm(), this.page());
  }

  private load(folder: MailView, search: string, page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.mailService.list({ folder, search, page, pageSize: PAGE_SIZE }).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not load your mail.'));
        this.loading.set(false);
      }
    });
  }
}
