import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { IconComponent } from '../../core/layout/icon.component';
import { ProjectWithCount } from '../../core/models';
import { ProjectService } from '../../core/services/project.service';
import { apiErrorMessage } from '../../shared/ui/api-error';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog.component';
import { ToastService } from '../../shared/ui/toast.service';
import { ProjectFormDialogComponent } from './project-form-dialog.component';

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [RouterLink, IconComponent, ConfirmDialogComponent, ProjectFormDialogComponent],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.css'
})
export class ProjectListComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);
  private readonly formDialog = viewChild.required(ProjectFormDialogComponent);

  readonly projects = signal<ProjectWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly query = signal('');
  readonly deletingId = signal<number | null>(null);

  /** Managers may open the screen read-only; only projects.manage unlocks adding, editing and deleting. */
  readonly canManage = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.ProjectsManage);
  });

  /** The employee list screen is guarded by employees.manage, so only link to it when it opens. */
  readonly canOpenEmployeeList = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.EmployeesManage);
  });

  /** Client-side search over name and description. */
  readonly filtered = computed(() => {
    const term = this.query().trim().toLowerCase();
    if (!term) {
      return this.projects();
    }
    return this.projects().filter(
      (project) =>
        project.name.toLowerCase().includes(term) || (project.description ?? '').toLowerCase().includes(term)
    );
  });

  readonly countLabel = computed(() => {
    const count = this.projects().length;
    return count === 1 ? '1 project' : `${count} projects`;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.projectService.getAll().subscribe({
      next: (projects) => {
        this.projects.set(projects);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load projects. Check your connection and try again.');
        this.loading.set(false);
      }
    });
  }

  employeeLabel(count: number): string {
    return count === 1 ? 'employee' : 'employees';
  }

  openCreate(): void {
    this.formDialog().open(null);
  }

  openEdit(project: ProjectWithCount): void {
    this.formDialog().open(project);
  }

  async delete(project: ProjectWithCount): Promise<void> {
    if (project.employeeCount > 0) {
      return;
    }

    const confirmed = await this.confirmDialog().open({
      title: 'Delete project?',
      message: `"${project.name}" will be permanently deleted.`,
      confirmLabel: 'Delete',
      variant: 'danger'
    });
    if (!confirmed) {
      return;
    }

    this.deletingId.set(project.id);
    this.projectService.delete(project.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.toast.success(`${project.name} deleted`);
        this.load();
      },
      error: (err) => {
        this.deletingId.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not delete the project.'));
      }
    });
  }
}
