import { Component, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../../shared/auth/auth.service';
import { EmployeeScreenPermissions, Permissions } from '../../../../shared/auth/permissions';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { ProjectWithCount } from '../models/project.model';
import { ProjectService } from '../services/project.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { ProjectFormDialogComponent } from '../components/project-form-dialog.component';
import { ProjectTeamDialogComponent } from '../components/project-team-dialog.component';

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [RouterLink, IconComponent, ConfirmDialogComponent, ProjectFormDialogComponent, ProjectTeamDialogComponent],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.css'
})
export class ProjectListComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly authService = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = viewChild.required(ConfirmDialogComponent);
  private readonly formDialog = viewChild.required(ProjectFormDialogComponent);
  private readonly teamDialog = viewChild.required(ProjectTeamDialogComponent);

  readonly projects = signal<ProjectWithCount[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly query = signal('');
  readonly deletingId = signal<number | null>(null);

  /** Managers may open the screen read-only; only the create, edit, delete and members permissions unlock those actions. */
  readonly canManage = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.ProjectsCreate);
  });

  readonly canMembers = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.ProjectsMembers);
  });

  readonly canEdit = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.ProjectsEdit);
  });

  readonly canDelete = computed(() => {
    this.authService.currentUser();
    return this.authService.hasPermission(Permissions.ProjectsDelete);
  });

  /** The employee list screen needs an employee action permission, so only link to it when it opens. */
  readonly canOpenEmployeeList = computed(() => {
    this.authService.currentUser();
    return EmployeeScreenPermissions.some((key) => this.authService.hasPermission(key));
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

  /** The "Assign team" step: opened right after a project is created, and skippable. */
  openAssignTeam(project: { id: number; name: string }): void {
    this.teamDialog().open(project, true);
  }

  openTeam(project: ProjectWithCount): void {
    this.teamDialog().open(project, false);
  }

  async delete(project: ProjectWithCount): Promise<void> {
    const confirmed = await this.confirmDialog().open({
      title: 'Delete project?',
      message:
        project.employeeCount > 0
          ? `"${project.name}" will be permanently deleted and its ${project.employeeCount} ${this.employeeLabel(project.employeeCount)} will be taken off it.`
          : `"${project.name}" will be permanently deleted.`,
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
