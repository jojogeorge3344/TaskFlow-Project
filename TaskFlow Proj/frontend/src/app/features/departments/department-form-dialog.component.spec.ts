import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DepartmentService } from '../../core/services/department.service';
import { ToastService } from '../../shared/ui/toast.service';
import { DepartmentFormDialogComponent } from './department-form-dialog.component';

describe('DepartmentFormDialogComponent', () => {
  let createSpy: jasmine.Spy;
  let updateSpy: jasmine.Spy;
  let toastSpy: jasmine.Spy;
  let fixture: ComponentFixture<DepartmentFormDialogComponent>;

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function dialog(): HTMLDialogElement {
    return el().querySelector('dialog')!;
  }

  function setName(value: string): void {
    const input = el().querySelector('#department-name') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  function submit(): void {
    (el().querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    createSpy = jasmine.createSpy('create');
    updateSpy = jasmine.createSpy('update');

    await TestBed.configureTestingModule({
      imports: [DepartmentFormDialogComponent],
      providers: [{ provide: DepartmentService, useValue: { create: createSpy, update: updateSpy } }]
    }).compileComponents();

    toastSpy = spyOn(TestBed.inject(ToastService), 'success');
    fixture = TestBed.createComponent(DepartmentFormDialogComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    if (dialog().open) {
      dialog().close();
    }
  });

  it('opens as "Add department" with the name field focused', () => {
    fixture.componentInstance.open(null);
    fixture.detectChanges();

    expect(dialog().open).toBeTrue();
    expect(el().querySelector('h2')?.textContent).toContain('Add department');
    expect(document.activeElement?.id).toBe('department-name');
  });

  it('shows "Name is required" and saves nothing when the name is empty', () => {
    fixture.componentInstance.open(null);
    fixture.detectChanges();
    setName('   ');

    submit();

    expect(el().querySelector('.tf-field__error')?.textContent).toContain('Name is required.');
    expect(createSpy).not.toHaveBeenCalled();
    expect(dialog().open).toBeTrue();
  });

  it('keeps the dialog open and shows the server error for a duplicate name', () => {
    createSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, error: { error: 'Department name already exists.' } }))
    );
    fixture.componentInstance.open(null);
    fixture.detectChanges();
    setName('Finance');

    submit();

    expect(dialog().open).toBeTrue();
    expect(el().querySelector('.tf-alert')?.textContent).toContain('Department name already exists.');
    expect(toastSpy).not.toHaveBeenCalled();
  });

  it('updates an existing department, closes, toasts and emits saved', () => {
    const savedSpy = jasmine.createSpy('saved');
    fixture.componentInstance.saved.subscribe(savedSpy);
    updateSpy.and.returnValue(of({ id: 2, name: 'Finance & Accounts', description: null, createdAt: '2026-09-01' }));

    fixture.componentInstance.open({ id: 2, name: 'Finance', description: null, createdAt: '2026-09-01', managerCount: 1, employeeCount: 4 });
    fixture.detectChanges();

    expect(el().querySelector('h2')?.textContent).toContain('Edit department');
    expect((el().querySelector('#department-name') as HTMLInputElement).value).toBe('Finance');

    setName('Finance & Accounts');
    submit();

    expect(updateSpy).toHaveBeenCalledWith(2, { name: 'Finance & Accounts', description: null });
    expect(dialog().open).toBeFalse();
    expect(toastSpy).toHaveBeenCalledWith('Finance & Accounts updated');
    expect(savedSpy).toHaveBeenCalled();
  });
});
