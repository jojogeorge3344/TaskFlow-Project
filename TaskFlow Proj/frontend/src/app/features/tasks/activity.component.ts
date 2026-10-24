import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { ActivityEntry } from '../../core/models';
import { apiErrorMessage } from '../../shared/ui/api-error';
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
  'HoursLogged',
  'TimeLogged',
  'Administrative'
] as const;

@Component({
  selector: 'app-activity',
  standalone: true,
  imports: [RouterLink, IconComponent],
  templateUrl: './activity.component.html',
  styleUrl: './activity.component.css'
})
export class ActivityComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly taskService = inject(TaskService);

  taskId = 0;

  readonly entries = signal<ActivityEntry[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly eventType = signal('');
  readonly search = signal('');
  readonly from = signal('');
  readonly to = signal('');

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
          this.entries.set(entries);
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(apiErrorMessage(err, 'Could not load the activity history.'));
          this.loading.set(false);
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
    return entry.newValue ?? '';
  }
}