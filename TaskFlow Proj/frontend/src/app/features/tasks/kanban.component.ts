import { Component, inject, OnInit, signal } from '@angular/core';
import {
  CdkDrag,
  CdkDragDrop,
  CdkDropList,
  CdkDropListGroup,
  moveItemInArray,
  transferArrayItem
} from '@angular/cdk/drag-drop';
import { Router } from '@angular/router';
import { BOARD_STATUSES, TaskCard, TaskStatus } from '../../core/models';
import { IconComponent } from '../../core/layout/icon.component';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskService } from './task.service';

interface BoardColumn {
  status: TaskStatus;
  tasks: TaskCard[];
}

const STATUS_LABELS: Record<TaskStatus, string> = {
  ToDo: 'To Do',
  InProgress: 'In Progress',
  Blocked: 'Blocked',
  Completed: 'Completed',
  Cancelled: 'Cancelled'
};

@Component({
  selector: 'app-kanban',
  standalone: true,
  imports: [CdkDropList, CdkDrag, CdkDropListGroup, IconComponent],
  templateUrl: './kanban.component.html',
  styleUrl: './kanban.component.css'
})
export class KanbanComponent implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly columns = signal<BoardColumn[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.taskService.getBoard().subscribe({
      next: (tasks) => {
        this.columns.set(this.group(tasks));
        this.loading.set(false);
      },
      error: () => {
        this.columns.set(this.group([]));
        this.error.set('Could not load the board. Try again.');
        this.loading.set(false);
      }
    });
  }

  label(status: TaskStatus): string {
    return STATUS_LABELS[status];
  }

  taskCount(): number {
    return this.columns().reduce((total, column) => total + column.tasks.length, 0);
  }

  priorityClass(priority: string): string {
    return `kanban__priority--${priority.toLowerCase()}`;
  }

  deadlineLabel(iso: string): string {
    return new Date(iso).toLocaleDateString();
  }

  open(task: TaskCard): void {
    this.router.navigate(['/tasks', task.id]);
  }

  drop(event: CdkDragDrop<TaskCard[]>, targetStatus: TaskStatus): void {
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
      this.refresh(event.container.data);
      return;
    }

    const source = this.columns().find((column) => column.tasks === event.previousContainer.data);
    const target = this.columns().find((column) => column.tasks === event.container.data);
    if (!source || !target || source.status === target.status) {
      return;
    }

    const card = event.item.data as TaskCard;
    const previousIndex = event.previousIndex;

    transferArrayItem(source.tasks, target.tasks, event.previousIndex, event.currentIndex);
    this.refresh(target.tasks);

    this.commitStatusChange(card, source.status, target.status, previousIndex);
  }

  private commitStatusChange(card: TaskCard, from: TaskStatus, to: TaskStatus, previousIndex: number): void {
    this.taskService.changeStatus(card.id, { status: to }).subscribe({
      next: () => {
        this.toast.success(`${card.taskCode} moved to ${STATUS_LABELS[to]}`);
      },
      error: (error: unknown) => {
        this.toast.error(apiErrorMessage(error, `Could not move ${card.taskCode}.`));
        this.rollback(card, from, to, previousIndex);
      }
    });
  }

  /** Confirmed 4xx responses roll the card back to its original column and position. */
  private rollback(card: TaskCard, from: TaskStatus, to: TaskStatus, previousIndex: number): void {
    this.columns.update((columns) =>
      columns.map((column) => {
        if (column.status === to) {
          return { ...column, tasks: column.tasks.filter((candidate) => candidate.id !== card.id) };
        }
        if (column.status === from) {
          const copy = [...column.tasks];
          copy.splice(Math.min(previousIndex, copy.length), 0, card);
          return { ...column, tasks: copy };
        }
        return column;
      })
    );
  }

  /** Force change detection on the array the drop mutated. */
  private refresh(tasks: TaskCard[]): void {
    this.columns.update((columns) => columns.map((column) => ({ ...column, tasks: [...column.tasks] })));
    void tasks;
  }

  private group(tasks: TaskCard[]): BoardColumn[] {
    return BOARD_STATUSES.map((status) => ({
      status,
      tasks: tasks.filter((task) => task.status === status)
    }));
  }
}