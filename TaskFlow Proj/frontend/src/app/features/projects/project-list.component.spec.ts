import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { ProjectWithCount } from '../../core/models';
import { ProjectService } from '../../core/services/project.service';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { ToastService } from '../../shared/ui/toast.service';
import { ProjectListComponent } from './project-list.component';

describe('ProjectListComponent', () => {
  const projects: ProjectWithCount[] = [
    { id: 1, name: 'Engineering', description: 'Builds the platform', createdAt: '2026-09-01', employeeCount: 12 },
    { id: 2, name: 'Finance', description: null, createdAt: '2026-09-01', employeeCount: 0 },
    { id: 3, name: 'Operations', description: 'Handles financial reporting', createdAt: '2026-09-01', employeeCount: 4 }
  ];

  let getAllSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;
  const currentUser = signal<{ permissions: string[] } | null>({
    permissions: systemPermissions('Administrator')
  });

  function create(): ComponentFixture<ProjectListComponent> {
    const fixture = TestBed.createComponent(ProjectListComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ProjectListComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function card(fixture: ComponentFixture<ProjectListComponent>, name: string): HTMLElement {
    return Array.from(el(fixture).querySelectorAll<HTMLElement>('article.project')).find(
      (node) => node.querySelector('.project__name')?.textContent?.trim() === name
    )!;
  }

  beforeEach(async () => {
    currentUser.set({ permissions: systemPermissions('Administrator') });
    getAllSpy = jasmine.createSpy('getAll').and.returnValue(of(projects));
    deleteSpy = jasmine.createSpy('delete').and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [ProjectListComponent],
      providers: [
        provideRouter([]),
        { provide: ProjectService, useValue: { getAll: getAllSpy, delete: deleteSpy } },
        { provide: AuthService, useValue: fakeAuthService(currentUser) }
      ]
    }).compileComponents();
  });

  afterEach(() => {
    document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close());
  });

  it('shows the header count and one card per project', () => {
    const fixture = create();

    expect(el(fixture).querySelector('.tf-page-header p')?.textContent).toContain('3 projects');
    expect(el(fixture).querySelectorAll('article.project').length).toBe(3);
    expect(card(fixture, 'Engineering').textContent).toContain('12');
    expect(card(fixture, 'Engineering').textContent).toContain('employees');
    expect(card(fixture, 'Finance').querySelector('.project__desc--none')?.textContent).toContain('No description');
  });

  it('filters by name or description, case-insensitively, without calling the server', () => {
    const fixture = create();
    getAllSpy.calls.reset();

    const input = el(fixture).querySelector('#project-search') as HTMLInputElement;
    input.value = 'FIN';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const names = Array.from(el(fixture).querySelectorAll('.project__name')).map((node) => node.textContent?.trim());
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

    expect(link.getAttribute('href')).toBe('/employees?projectId=3');
  });

  it('guards Delete for a project that still has employees', fakeAsync(() => {
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

  it('deletes an empty project after confirmation and shows a toast', fakeAsync(() => {
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

  it('shows an empty state when no projects exist', () => {
    getAllSpy.and.returnValue(of([]));
    const fixture = create();

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No projects yet');
  });

  it('gives a view-only user the list without Add, Edit or Delete controls', () => {
    currentUser.set({ permissions: [Permissions.ProjectsView] });
    const fixture = create();

    expect(el(fixture).querySelector('.tf-page-header .tf-btn--primary')).toBeNull();
    expect(el(fixture).querySelectorAll('[data-action="edit"]').length).toBe(0);
    expect(el(fixture).querySelectorAll('[data-action="delete"]').length).toBe(0);
    expect(card(fixture, 'Operations').querySelector('a')).toBeNull();
    expect(card(fixture, 'Engineering').textContent).toContain('12');
  });

  it('does not offer Add in the empty state to a view-only user', () => {
    currentUser.set({ permissions: [Permissions.ProjectsView] });
    getAllSpy.and.returnValue(of([]));
    const fixture = create();

    expect(el(fixture).querySelector('.tf-empty')?.textContent).toContain('No projects yet');
    expect(el(fixture).querySelector('.tf-empty button')).toBeNull();
  });

  it('shows an error with Retry when loading fails', () => {
    getAllSpy.and.returnValue(throwError(() => new Error('offline')));
    const fixture = create();

    expect(el(fixture).querySelector('[role="alert"]')?.textContent).toContain('Could not load projects');

    getAllSpy.and.returnValue(of(projects));
    (el(fixture).querySelector('[role="alert"] button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el(fixture).querySelectorAll('article.project').length).toBe(3);
  });
});
