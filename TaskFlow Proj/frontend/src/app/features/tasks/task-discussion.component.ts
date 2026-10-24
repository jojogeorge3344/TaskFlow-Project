import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { TaskComment } from '../../core/models';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskService } from './task.service';

@Component({
  selector: 'app-task-discussion',
  standalone: true,
  imports: [RouterLink, IconComponent],
  templateUrl: './task-discussion.component.html',
  styleUrl: './task-discussion.component.css'
})
export class TaskDiscussionComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly taskService = inject(TaskService);
  private readonly toast = inject(ToastService);

  readonly taskId = Number(this.route.snapshot.paramMap.get('id'));

  /** Reading a discussion is open to anyone who can see the task; writing in it needs its own permission. */
  get canComment(): boolean {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.TasksCommentCreate);
  }

  readonly currentUserName = this.authService.currentUser()?.fullName ?? 'You';
  readonly comments = signal<TaskComment[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly body = signal('');
  readonly posting = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

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

  timeLabel(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('') || '?';
  }
}