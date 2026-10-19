import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DepartmentWithCount } from '../../core/models';
import { DepartmentService } from '../../core/services/department.service';
import { ToastService } from '../../shared/ui/toast.service';
import { DepartmentListComponent } from './department-list.component';

describe('DepartmentListComponent', () => {
  const departments: DepartmentWithCount[] = [
    { id: 1, name: 'Engineering', description: 'Builds the platform', createdAt: '2026-09-01', employeeCount: 12 },
    { id: 2, name: 'Finance', description: null, createdAt: '2026-09-01', employeeCount: 0 },
    { id: 3, name: 'Operations', description: 'Handles financial reporting', createdAt: '2026-09-01', employeeCount: 4 }
  ];

  let getAllSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;

  function create(): ComponentFixture<DepartmentListComponent> {
    const fixture = TestBed.createComponent(DepartmentListComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<DepartmentListComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function card(fixture: ComponentFixture<DepartmentListComponent>, name: string): HTMLElement {
    return Array.from(el(fixture).querySelectorAll<HTMLElement>('article.dept')).find(
      (node) => node.querySelector('.dept__name')?.textContent?.trim() === name
    )!;
  }

  beforeEach(async () => {
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(departments));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [DepartmentListComponent],
      providers: [provideRouter([]), { provide: DepartmentService, useValue: { getAll: getAllSpy, delete: deleteSpy } }]
    }).compileComponents();
  });

  afterEach(() => {
    document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close());
  });

  it('shows the header count and one card per department', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.tf-page-header p')?.textContent).toContain('3 projects');
    expect(el(fixture).querySelectorAll('article.dept').length).toBe(3);
    expect(card(fixture, 'Engineering').textContent).toContain('12');
    expect(card(fixture, 'Engineering').textContent).toContain('employees');
    expect(card(fixture, 'Finance').querySelector('.dept__desc--none')?.textContent).toContain('No description');
  });

  it('filters by name or description, case-insensitively, without calling the server', () => {
    const fixture = create();
    getAllSpy.calls.reset();

    const input = el(fixture).querySelector('#dept-search') as HTMLInputElement;
    input.value = 'FIN';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const names = Array.from(el(fixture).querySelectorAll('.dept__name')).map((node) => node.textContent?.trim());
    expect(names).toEqual(['Finance', 'Operations']);
    expect(getAllSpy).not.toHaveBeenCalled();
  });

  it('shows a no-match message when the search matches nothing', () => {
    const fixture = create();
    fixture.componentInstance.query.set('zzz');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No projects match');
  });

  it('links View employees to the filtered employee list', () => {
    const fixture = create();
    const link = card(fixture, 'Operations').querySelector('a') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/employees?departmentId=3');
  });

  it('guards Delete for a department that still has employees', fakeAsync(() => {
    const fixture = create();
    const button = card(fixture, 'Engineering').querySelector('[data-action="delete"]') as HTMLButtonElement;

    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.title).toContain('Reassign its employees');
    const hint = el(fixture).querySelector(`#${button.getAttribute('aria-describedby')}`);
    expect(hint?.textContent).toContain('Reassign its 12 employees before deleting');

    button.click();
    fixture.detectChanges();
    flushMicrotasks();

    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeFalse();
    expect(deleteSpy).not.toHaveBeenCalled();
  }));

  it('deletes an empty department after confirmation and shows a toast', fakeAsync(() => {
    const fixture = create();
    const toastSpy = spyOn(TestBed.inject(ToastService), 'success');

    (card(fixture, 'Finance').querySelector('[data-action="delete"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.textContent).toContain('"Finance"');

    (dialog.querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(deleteSpy).toHaveBeenCalledWith(2);
    expect(toastSpy).toHaveBeenCalledWith('Finance deleted');
  }));

  it('does not delete when the confirmation is cancelled', fakeAsync(() => {
    const fixture = create();

    (card(fixture, 'Finance').querySelector('[data-action="delete"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    (el(fixture).querySelector('tf-confirm-dialog [data-action="cancel"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(deleteSpy).not.toHaveBeenCalled();
  }));

  it('shows an empty state when no departments exist', () => {
    getAllSpy.and.returnValue(of([]));
    const fixture = create();

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No projects yet');
  });

  it('shows an error with Retry when loading fails', () => {
    getAllSpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load projects');

    getAllSpy.and.returnValue(of(departments));
    (el(fixture).querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('article.dept').length).toBe(3);
  });
});
