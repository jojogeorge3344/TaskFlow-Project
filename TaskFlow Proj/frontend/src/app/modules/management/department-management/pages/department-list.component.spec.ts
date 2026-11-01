import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../shared/auth/auth.service';
import { DepartmentWithCount } from '../models/department.model';
import { DepartmentService } from '../services/department.service';
import { UserService } from '../../employee-management/services/user.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { fakeAuthService, systemPermissions } from '../../../../shared/testing/permissions';
import { DepartmentListComponent } from './department-list.component';
import { DepartmentMembersDialogComponent } from '../components/department-members-dialog.component';

describe('DepartmentListComponent', () => {
  const departments: DepartmentWithCount[] = [
    {
      id: 1,
      name: 'Engineering',
      description: 'Builds the platform',
      createdAt: '2026-09-01',
      managerCount: 2,
      employeeCount: 12
    },
    { id: 2, name: 'Finance', description: null, createdAt: '2026-09-01', managerCount: 0, employeeCount: 0 },
    {
      id: 3,
      name: 'Operations',
      description: 'Keeps the lights on',
      createdAt: '2026-09-01',
      managerCount: 1,
      employeeCount: 0
    }
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
    return Array.from(el(fixture).querySelectorAll<HTMLElement>('article.department')).find(
      (node) => node.querySelector('.department__name')?.textContent?.trim() === name
    )!;
  }

  beforeEach(async () => {
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(departments));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [DepartmentListComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: fakeAuthService(() => ({ permissions: systemPermissions('Administrator') })) },
        {
          provide: DepartmentService,
          useValue: {
            getAll: getAllSpy,
            remove: deleteSpy,
            getMembers: () => of({ departmentId: 1, managers: [], employees: [] }),
            replaceMembers: () => of({ departmentId: 1, managers: [], employees: [] })
          }
        },
        { provide: UserService, useValue: { getAll: () => of([]) } }
      ]
    }).compileComponents();
  });

  afterEach(() => {
    document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close());
  });

  it('shows the header count and one card per department with its member counts', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.tf-page-header p')?.textContent).toContain('3 departments');
    expect(el(fixture).querySelectorAll('article.department').length).toBe(3);
    expect(card(fixture, 'Engineering').textContent).toContain('2');
    expect(card(fixture, 'Engineering').textContent).toContain('managers');
    expect(card(fixture, 'Engineering').textContent).toContain('12');
    expect(card(fixture, 'Engineering').textContent).toContain('employees');
    expect(card(fixture, 'Finance').querySelector('.department__desc--none')?.textContent).toContain('No description');
  });

  it('filters by name, case-insensitively, without calling the server', () => {
    const fixture = create();
    getAllSpy.calls.reset();

    const input = el(fixture).querySelector('#department-search') as HTMLInputElement;
    input.value = 'fin';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const names = Array.from(el(fixture).querySelectorAll('.department__name')).map((node) => node.textContent?.trim());
    expect(names).toEqual(['Finance']);
    expect(getAllSpy).not.toHaveBeenCalled();
  });

  it('shows a no-match message when the search matches nothing', () => {
    const fixture = create();
    fixture.componentInstance.query.set('zzz');
    fixture.detectChanges();

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No departments match');
  });

  it('opens the form dialog for a new department from the header button', () => {
    const fixture = create();

    (el(fixture).querySelector('.tf-page-header button') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('app-department-form-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.querySelector('h2')?.textContent).toContain('Add department');
  });

  it('opens the form dialog prefilled from a card edit button', () => {
    const fixture = create();

    (card(fixture, 'Finance').querySelector('[data-action="edit"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('app-department-form-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.querySelector('h2')?.textContent).toContain('Edit department');
    expect((dialog.querySelector('#department-name') as HTMLInputElement).value).toBe('Finance');
  });

  it('offers member management as an icon button with a tooltip, not a text link', () => {
    const fixture = create();

    const button = card(fixture, 'Engineering').querySelector('[data-action="members"]') as HTMLButtonElement;
    expect(button.title).toBe('Manage members');
    expect(button.textContent?.trim()).toBe('');
    expect(button.querySelector('svg')).toBeTruthy();
  });

  it('opens the members dialog from a card and reloads the list when it saves', () => {
    const fixture = create();

    (card(fixture, 'Engineering').querySelector('[aria-label="Manage members of Engineering"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('app-department-members-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.textContent).toContain('Manage members');

    getAllSpy.calls.reset();
    const membersDialog = fixture.debugElement.query(By.directive(DepartmentMembersDialogComponent));
    membersDialog.componentInstance.saved.emit({ departmentId: 1, managers: [], employees: [] });
    fixture.detectChanges();

    expect(getAllSpy).toHaveBeenCalled();
  });

  it('guards Delete for a department that still has members', fakeAsync(() => {
    const fixture = create();
    const button = card(fixture, 'Engineering').querySelector('[data-action="delete"]') as HTMLButtonElement;

    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.title).toContain('Remove its managers and employees');
    const hint = el(fixture).querySelector(`#${button.getAttribute('aria-describedby')}`);
    expect(hint?.textContent).toContain('Remove its 2 managers and 12 employees before deleting');

    button.click();
    fixture.detectChanges();
    flushMicrotasks();

    expect((el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement).open).toBeFalse();
    expect(deleteSpy).not.toHaveBeenCalled();
  }));

  it('guards Delete for a department that only still has managers', () => {
    const fixture = create();
    const button = card(fixture, 'Operations').querySelector('[data-action="delete"]') as HTMLButtonElement;

    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.title).toContain('Remove its managers and employees');
  });

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

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No departments yet');
  });

  it('shows an error with Retry when loading fails', () => {
    getAllSpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load departments');

    getAllSpy.and.returnValue(of(departments));
    (el(fixture).querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('article.department').length).toBe(3);
  });
});
