import { Component, ElementRef, inject, output, signal, viewChild } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { Project, ProjectWithCount } from '../models/project.model';
import { ProjectService } from '../services/project.service';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ToastService } from '../../../../shared/services/toast.service';

function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** Add/edit project modal on the native <dialog>. Errors stay inside the dialog; success closes it. */
@Component({
  selector: 'app-project-form-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  template: `
    <dialog #dialog class="tf-modal" aria-labelledby="project-dialog-title" (cancel)="onCancelEvent($event)">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <header class="tf-modal__head">
          <h2 id="project-dialog-title">{{ editing() ? 'Edit project' : 'Add project' }}</h2>
          <button type="button" class="tf-btn tf-btn--icon" aria-label="Close dialog" (click)="close()">
            <tf-icon name="x" />
          </button>
        </header>

        <div class="tf-modal__body">
          @if (serverError()) {
            <div class="tf-alert" role="alert">{{ serverError() }}</div>
          }
          <div class="tf-field">
            <label for="project-name">Name <span class="tf-field__required" aria-hidden="true">*</span></label>
            <input
              #nameInput
              id="project-name"
              class="tf-input"
              formControlName="name"
              autocomplete="off"
              [class.is-invalid]="showNameError()"
              [attr.aria-invalid]="showNameError()"
              aria-describedby="project-name-error"
            />
            @if (showNameError()) {
              <span id="project-name-error" class="tf-field__error">Name is required.</span>
            }
          </div>
          <div class="tf-field">
            <label for="project-description">Description</label>
            <textarea id="project-description" class="tf-textarea" formControlName="description"></textarea>
          </div>
        </div>

        <footer class="tf-modal__foot">
          <button type="button" class="tf-btn tf-btn--ghost" (click)="close()">Cancel</button>
          <button type="submit" class="tf-btn tf-btn--primary" [disabled]="saving()">
            {{ saving() ? 'Saving…' : editing() ? 'Save changes' : 'Add project' }}
          </button>
        </footer>
      </form>
    </dialog>
  `
})
export class ProjectFormDialogComponent {
  private readonly projectService = inject(ProjectService);
  private readonly toast = inject(ToastService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly nameInput = viewChild.required<ElementRef<HTMLInputElement>>('nameInput');

  readonly saved = output<Project>();
  /** Emits only when a new project was added, so the screen can go straight on to assigning its team. */
  readonly created = output<Project>();

  readonly editing = signal<ProjectWithCount | null>(null);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    description: new FormControl('', { nonNullable: true })
  });

  open(project: ProjectWithCount | null): void {
    this.editing.set(project);
    this.saving.set(false);
    this.submitted.set(false);
    this.serverError.set(null);
    this.form.reset({ name: project?.name ?? '', description: project?.description ?? '' });

    const dialog = this.dialog().nativeElement;
    if (!dialog.open) {
      dialog.showModal();
    }
    this.nameInput().nativeElement.focus();
  }

  close(): void {
    const dialog = this.dialog().nativeElement;
    if (dialog.open) {
      dialog.close();
    }
  }

  onCancelEvent(event: Event): void {
    event.preventDefault();
    this.close();
  }

  showNameError(): boolean {
    const name = this.form.controls.name;
    return name.invalid && (name.touched || this.submitted());
  }

  submit(): void {
    this.submitted.set(true);
    this.serverError.set(null);
    if (this.form.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    const value = this.form.getRawValue();
    const request = { name: value.name.trim(), description: value.description.trim() || null };
    const project = this.editing();
    const request$ = project
      ? this.projectService.update(project.id, request)
      : this.projectService.create(request);

    request$.subscribe({
      next: (result) => {
        this.saving.set(false);
        this.toast.success(project ? `${result.name} updated` : `${result.name} added`);
        this.close();
        this.saved.emit(result);
        if (!project) {
          this.created.emit(result);
        }
      },
      error: (err) => {
        this.saving.set(false);
        this.serverError.set(apiErrorMessage(err, project ? 'Could not update the project.' : 'Could not add the project.'));
      }
    });
  }
}
