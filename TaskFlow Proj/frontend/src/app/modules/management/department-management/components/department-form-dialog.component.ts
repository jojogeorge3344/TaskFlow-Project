import { Component, ElementRef, inject, output, signal, viewChild } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidationErrors } from '@angular/forms';
import { Department, DepartmentWithCount } from '../models/department.model';
import { DepartmentService } from '../services/department.service';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { ToastService } from '../../../../shared/services/toast.service';

function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value?.trim() ? null : { required: true };
}

/** Add/edit department modal on the native <dialog>. Errors stay inside the dialog; success closes it. */
@Component({
  selector: 'app-department-form-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, IconComponent],
  template: `
    <dialog #dialog class="tf-modal" aria-labelledby="department-dialog-title" (cancel)="onCancelEvent($event)">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <header class="tf-modal__head">
          <h2 id="department-dialog-title">{{ editing() ? 'Edit department' : 'Add department' }}</h2>
          <button type="button" class="tf-btn tf-btn--icon" aria-label="Close dialog" (click)="close()">
            <tf-icon name="x" />
          </button>
        </header>

        <div class="tf-modal__body">
          @if (serverError()) {
            <div class="tf-alert" role="alert">{{ serverError() }}</div>
          }
          <div class="tf-field">
            <label for="department-name">Name <span class="tf-field__required" aria-hidden="true">*</span></label>
            <input
              #nameInput
              id="department-name"
              class="tf-input"
              formControlName="name"
              autocomplete="off"
              [class.is-invalid]="showNameError()"
              [attr.aria-invalid]="showNameError()"
              aria-describedby="department-name-error"
            />
            @if (showNameError()) {
              <span id="department-name-error" class="tf-field__error">Name is required.</span>
            }
          </div>
          <div class="tf-field">
            <label for="department-description">Description</label>
            <textarea id="department-description" class="tf-textarea" formControlName="description"></textarea>
          </div>
        </div>

        <footer class="tf-modal__foot">
          <button type="button" class="tf-btn tf-btn--ghost" (click)="close()">Cancel</button>
          <button type="submit" class="tf-btn tf-btn--primary" [disabled]="saving()">
            {{ saving() ? 'Saving…' : editing() ? 'Save changes' : 'Add department' }}
          </button>
        </footer>
      </form>
    </dialog>
  `
})
export class DepartmentFormDialogComponent {
  private readonly departmentService = inject(DepartmentService);
  private readonly toast = inject(ToastService);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly nameInput = viewChild.required<ElementRef<HTMLInputElement>>('nameInput');

  readonly saved = output<Department>();

  readonly editing = signal<DepartmentWithCount | null>(null);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [requiredText] }),
    description: new FormControl('', { nonNullable: true })
  });

  open(department: DepartmentWithCount | null): void {
    this.editing.set(department);
    this.saving.set(false);
    this.submitted.set(false);
    this.serverError.set(null);
    this.form.reset({ name: department?.name ?? '', description: department?.description ?? '' });

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
    const department = this.editing();
    const request$ = department
      ? this.departmentService.update(department.id, request)
      : this.departmentService.create(request);

    request$.subscribe({
      next: (result) => {
        this.saving.set(false);
        this.toast.success(department ? `${result.name} updated` : `${result.name} added`);
        this.close();
        this.saved.emit(result);
      },
      error: (err) => {
        this.saving.set(false);
        this.serverError.set(
          apiErrorMessage(err, department ? 'Could not update the department.' : 'Could not add the department.')
        );
      }
    });
  }
}
