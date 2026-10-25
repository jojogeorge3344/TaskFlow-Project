import { FormsModule } from '@angular/forms';
import { Component, inject, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { IconComponent } from '../../core/layout/icon.component';
import { ActivityEntry } from '../../core/models';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ActivityEventMeta, eventMeta } from './activity-meta';
import { initials } from '../../shared/ui/initials';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskService } from './task.service';

export const ACTIVITY_EVENT_TYPES = [
  'Created',
  'Assigned',
  'Reassigned',
  'StatusChanged',
  'DeadlineChanged',
  'PriorityChanged',
  'Completed',
  'CommentAdded',
  'CommentEdited',
  'CommentDeleted',
  'HoursLogged',
  'TimeLogged',
  'Administrative'
] as const;

@Component({
  selector: 'app-activity',
  standalone: true,
  imports: [FormsModule, RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './activity.component.html',
  styleUrl: './activity.component.css'
})
export class ActivityComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly taskService = inject(TaskService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  taskId = 0;

  readonly entries = signal<ActivityEntry[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly eventType = signal('');
  readonly search = signal('');
  readonly from = signal('');
  readonly to = signal('');
  readonly editingId = signal<number | null>(null);
  readonly editHours = signal('');
  readonly saving = signal(false);

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (Number.isFinite(id) && id > 0) {
      this.taskId = id;
    } else {
      this.route.paramMap.subscribe((params) => {
        const parsed = Number(params.get('id'));
        if (Number.isFinite(parsed) && parsed > 0 && parsed !== this.taskId) {
          this.taskId = parsed;
          this.load();
        }
      });
    }
    const query = this.route.snapshot.queryParamMap;
    this.eventType.set(query.get('eventType') ?? '');
    this.search.set(query.get('search') ?? '');
    this.from.set(query.get('from') ?? '');
    this.to.set(query.get('to') ?? '');
    if (this.taskId) {
      this.load();
    } else {
      this.loading.set(false);
      this.error.set('Invalid task id.');
    }
  }

  load(): void {
    if (!this.taskId) {
      return;
    }
    this.loading.set(true);
    this.error.set(null);

    this.taskService
      .getActivity(this.taskId, {
        eventType: this.eventType() || undefined,
        search: this.search() || undefined,
        from: this.from() || undefined,
        to: this.to() || undefined
      })
      .subscribe({
        next: (entries) => {
          // The API returns oldest first; the history reads newest first.
          this.entries.set([...entries].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime() || b.id - a.id));
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(apiErrorMessage(err, 'Could not load the activity history.'));
          this.loading.set(false);
        }
      });
  }

  /** The event types that get a quick filter pill; every other type is in the "Other events" list. */
  readonly commonTypes = ['StatusChanged', 'CommentAdded', 'HoursLogged', 'Reassigned', 'DeadlineChanged'];

  otherTypes(): string[] {
    return this.eventTypes().filter((type) => !this.commonTypes.includes(type));
  }

  /** Entries grouped by the local day they happened, newest day first (the list is already newest first). */
  dayGroups(): { label: string; entries: ActivityEntry[] }[] {
    const groups: { label: string; entries: ActivityEntry[] }[] = [];
    for (const entry of this.entries()) {
      const label = this.dayLabel(entry.createdAt);
      const last = groups[groups.length - 1];
      if (last && last.label === label) {
        last.entries.push(entry);
      } else {
        groups.push({ label, entries: [entry] });
      }
    }
    return groups;
  }

  dayLabel(iso: string): string {
    const date = new Date(iso);
    const today = new Date();
    const yesterday = new Date();
    yesterday.setDate(today.getDate() - 1);
    if (date.toDateString() === today.toDateString()) {
      return 'Today';
    }
    if (date.toDateString() === yesterday.toDateString()) {
      return 'Yesterday';
    }
    return date.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  }

  clockLabel(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  }

  /** A readable name, colour and icon for each kind of event. */
  eventMeta(type: string): ActivityEventMeta {
    return eventMeta(type);
  }

  clearType(): void {
    this.eventType.set('');
    this.load();
  }

  selectOther(type: string): void {
    this.eventType.set(type);
    this.load();
  }

  /** Picks one event type from a filter pill and reloads; picking the active pill again shows everything. */
  pickType(type: string): void {
    this.eventType.set(this.eventType() === type ? '' : type);
    this.load();
  }

  initialsOf(name: string | null | undefined): string {
    return initials(name);
  }

  /** Only the Administrator role may correct or remove tracked-hours entries. */
  canManageHours(entry: ActivityEntry): boolean {
    return entry.eventType === 'HoursLogged' && this.authService.currentUser()?.role === 'Administrator';
  }

  /** The hours one entry added: its new worked total minus the total before it. */
  entryHours(entry: ActivityEntry): number {
    return Math.round((Number(entry.newValue) - Number(entry.oldValue)) * 100) / 100;
  }

  startEditHours(entry: ActivityEntry): void {
    this.editingId.set(entry.id);
    this.editHours.set(String(this.entryHours(entry)));
  }

  cancelEditHours(): void {
    this.editingId.set(null);
    this.editHours.set('');
  }

  saveHours(entry: ActivityEntry): void {
    const hours = Number(this.editHours());
    if (!Number.isFinite(hours) || hours <= 0) {
      this.toast.error('Enter the hours (greater than 0).');
      return;
    }
    if (this.saving()) {
      return;
    }

    this.saving.set(true);
    this.taskService.updateHoursEntry(this.taskId, entry.id, hours).subscribe({
      next: () => {
        this.saving.set(false);
        this.cancelEditHours();
        this.toast.success('Hours updated');
        this.load();
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Could not update the hours.'));
      }
    });
  }

  async deleteHours(entry: ActivityEntry): Promise<void> {
    const confirmed = await this.confirmDialog().open({
      title: 'Delete hours entry?',
      message: `This removes ${this.entryHours(entry)}h from the task's worked hours.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.taskService.deleteHoursEntry(this.taskId, entry.id).subscribe({
      next: () => {
        this.toast.success('Hours entry deleted');
        this.load();
      },
      error: (err: unknown) => {
        this.toast.error(apiErrorMessage(err, 'Could not delete the hours entry.'));
      }
    });
  }

  clearFilters(): void {
    this.eventType.set('');
    this.search.set('');
    this.from.set('');
    this.to.set('');
    this.load();
  }

  eventTypes(): string[] {
    return [...ACTIVITY_EVENT_TYPES];
  }

  timeLabel(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  /** Compact "old → new" summary used by the column headers. */
  changeLabel(entry: ActivityEntry): string {
    if (entry.oldValue && entry.newValue) {
      return `${entry.oldValue} → ${entry.newValue}`;
    }
    return entry.newValue ?? entry.oldValue ?? '';
  }
}