import { Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IconComponent } from '../../core/layout/icon.component';
import { TaskDetails, User } from '../../core/models';
import { UserService } from '../../core/services/user.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskService } from './task.service';

@Component({
  selector: 'app-task-reassign',
  standalone: true,
  imports: [RouterLink, IconComponent, ConfirmDialogComponent],
  templateUrl: './reassign.component.html',
  styleUrl: './reassign.component.css'
})
export class TaskReassignComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly taskService = inject(TaskService);
  private readonly userService = inject(UserService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);

  readonly taskId = Number(this.route.snapshot.paramMap.get('id'));

  readonly task = signal<TaskDetails | null>(null);
  readonly employees = signal<User[]>([]);
  readonly employeesLoading = signal(true);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selectedUserId = signal<number | null>(null);
  readonly reason = signal('');
  readonly submitting = signal(false);

  readonly canSubmit = computed(() => this.selectedUserId() !== null && this.reason().trim().length > 0);

  readonly eligibleEmployees = computed(() =>
    this.employees().filter(
      (employee) => employee.isActive && employee.id !== this.task()?.assignedToUserId
    )
  );

  ngOnInit(): void {
    this.loadTask();
    this.loadEmployees();
  }

  loadTask(): void {
    this.loading.set(true);
    this.error.set(null);

    this.taskService.getById(this.taskId).subscribe({
      next: (details) => {
        this.task.set(details);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(apiErrorMessage(err, 'Could not load the task.'));
        this.loading.set(false);
      }
    });
  }

  private loadEmployees(): void {
    this.employeesLoading.set(true);

    this.userService.getAll({ role: 'Employee', isActive: true }).subscribe({
      next: (employees) => {
        this.employees.set(employees);
        this.employeesLoading.set(false);
      },
      error: () => {
        this.employees.set([]);
        this.employeesLoading.set(false);
      }
    });
  }

  /** Parses the employee <select> value into a numeric id (null when empty or invalid). */
  onAssigneeChange(value: unknown): void {
    const parsed = typeof value === 'number' ? value : Number(value ?? NaN);
    this.selectedUserId.set(Number.isFinite(parsed) && parsed > 0 ? parsed : null);
  }

  confirm(): void {
    const task = this.task();
    const newAssigneeUserId = this.selectedUserId();
    if (!task || !newAssigneeUserId || this.submitting()) {
      return;
    }

    const assignee = this.employees().find((employee) => employee.id === newAssigneeUserId);
    void this.confirmDialog()
      .open({
        title: 'Reassign task?',
        message: `${task.taskCode} “${task.title}” will move from ${task.assignedToName || 'the current assignee'} to ${assignee?.fullName ?? 'the selected employee'}. The new assignee will be notified.`,
        confirmLabel: 'Reassign'
      })
      .then((confirmed) => {
        if (!confirmed) {
          return;
        }

        this.submitting.set(true);
        this.taskService
          .reassign(task.id, { newAssigneeUserId, reason: this.reason().trim() })
          .subscribe({
            next: () => {
              this.submitting.set(false);
              this.toast.success('Task reassigned');
              this.router.navigate(['/tasks', task.id]);
            },
            error: (err: unknown) => {
              this.submitting.set(false);
              this.toast.error(apiErrorMessage(err, 'Could not reassign the task.'));
            }
          });
      });
  }

  cancel(): void {
    this.router.navigate(['/tasks', this.taskId]);
  }
}