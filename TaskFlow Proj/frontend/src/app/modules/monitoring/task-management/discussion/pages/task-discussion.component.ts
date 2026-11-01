import { FormsModule } from '@angular/forms';
import { Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../../../shared/auth/auth.service';
import { Permissions } from '../../../../../shared/auth/permissions';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { TaskComment, TaskDetails } from '../../models/task.model';
import { apiErrorMessage } from '../../../../../shared/utils/api-error';
import { ConfirmDialogComponent } from '../../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { initials as initialsOf } from '../../../../../shared/utils/initials';
import { ToastService } from '../../../../../shared/services/toast.service';
import { TaskService } from '../../services/task.service';

@Component({
  selector: 'app-task-discussion',
  standalone: true,
  imports: [FormsModule, RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './task-discussion.component.html',
  styleUrl: './task-discussion.component.css'
})
export class TaskDiscussionComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly taskService = inject(TaskService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  taskId = 0;

  /** Reading a discussion is open to anyone who can see the task; writing in it needs its own permission. */
  get canComment(): boolean {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksCommentCreate);
  }

  /** Edit and delete are the author's own, and each needs its own permission. */
  canEdit(comment: TaskComment): boolean {
    this.authService.currentUser();
    return comment.authorUserId === this.currentUserId() && this.authService.hasPermission(Permissions.TasksCommentEdit);
  }

  canDelete(comment: TaskComment): boolean {
    this.authService.currentUser();
    return comment.authorUserId === this.currentUserId() && this.authService.hasPermission(Permissions.TasksCommentDelete);
  }

  private currentUserId(): number | undefined {
    return this.authService.currentUser()?.id;
  }

  readonly currentUserName = this.authService.currentUser()?.fullName ?? 'You';
  readonly comments = signal<TaskComment[]>([]);

  readonly task = signal<TaskDetails | null>(null);

  /** Comments grouped by the local day they were written, oldest day first. */
  readonly dayGroups = computed(() => {
    const groups: { label: string; comments: TaskComment[] }[] = [];
    for (const comment of this.comments()) {
      const label = this.dayLabel(comment.createdAt);
      const last = groups[groups.length - 1];
      if (last && last.label === label) {
        last.comments.push(comment);
      } else {
        groups.push({ label, comments: [comment] });
      }
    }
    return groups;
  });

  isMine(comment: TaskComment): boolean {
    return comment.authorUserId === this.currentUserId();
  }

  /** The task's other recipients, without the assignee (shown first on their own). */
  otherRecipients(task: TaskDetails): TaskDetails['recipients'] {
    return task.recipients.filter((recipient) => recipient.userId !== task.assignedToUserId);
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

  dateLabel(iso: string): string {
    return new Date(iso).toLocaleDateString();
  }
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly body = signal('');
  readonly posting = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editBody = signal('');
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

    this.taskService.getById(this.taskId).subscribe({
      next: (task) => this.task.set(task),
      error: () => this.task.set(null)
    });

    this.taskService.getComments(this.taskId).subscribe({
      next: (comments) => {
        this.comments.set(comments);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not load the discussion.'));
        this.loading.set(false);
      }
    });
  }

  /** Comments render oldest first; the newest appears at the bottom. */
  post(): void {
    const body = this.body().trim();
    if (!body || this.posting()) {
      return;
    }

    this.posting.set(true);
    this.taskService.addComment(this.taskId, { body }).subscribe({
      next: (comment) => {
        this.posting.set(false);
        this.comments.update((comments) => [...comments, comment]);
        this.body.set('');
        this.toast.success('Comment posted');
      },
      error: (err: unknown) => {
        this.posting.set(false);
        this.toast.error(apiErrorMessage(err, 'Could not post the comment.'));
      }
    });
  }

  startEdit(comment: TaskComment): void {
    this.editingId.set(comment.id);
    this.editBody.set(comment.body);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editBody.set('');
  }

  saveEdit(comment: TaskComment): void {
    const body = this.editBody().trim();
    if (!body || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.taskService.updateComment(this.taskId, comment.id, { body }).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.comments.update((comments) => comments.map((existing) => (existing.id === updated.id ? updated : existing)));
        this.cancelEdit();
        this.toast.success('Comment updated');
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Could not update the comment.'));
      }
    });
  }

  async remove(comment: TaskComment): Promise<void> {
    const confirmed = await this.confirmDialog().open({
      title: 'Delete comment?',
      message: 'This comment will be removed from the discussion for everyone.',
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.taskService.deleteComment(this.taskId, comment.id).subscribe({
      next: () => {
        this.comments.update((comments) => comments.filter((existing) => existing.id !== comment.id));
        this.toast.success('Comment deleted');
      },
      error: (err: unknown) => {
        this.toast.error(apiErrorMessage(err, 'Could not delete the comment.'));
      }
    });
  }

  timeLabel(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  initials(name: string | null | undefined): string {
    return initialsOf(name);
  }
}