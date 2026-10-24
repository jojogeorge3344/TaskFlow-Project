import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Permissions } from '../../core/auth/permissions';
import { ProjectWithCount } from '../../core/models';
import { PositionService } from '../../core/services/position.service';
import { ProjectService } from '../../core/services/project.service';
import { UserService } from '../../core/services/user.service';
import { fakeAuthService, systemPermissions } from '../../testing/permissions';
import { ToastService } from '../../shared/ui/toast.service';
import { ProjectListComponent } from './project-list.component';

describe('ProjectListComponent', () => {
  const projects: ProjectWithCount[] = [
    { id: 1, name: 'Engineering', description: 'Builds the platform', createdAt: '2026-09-01', employeeCount: 12, managers: [] },
    { id: 2, name: 'Finance', description: null, createdAt: '2026-09-01', employeeCount: 0, managers: [] },
    { id: 3, name: 'Operations', description: 'Handles financial reporting', createdAt: '2026-09-01', employeeCount: 4, managers: [] }
  ];

  let getAllSpy: jasmine.Spy;
  let deleteSpy: jasmine.Spy;
  let getMembersSpy: jasmine.Spy;
  let replaceMembersSpy: jasmine.Spy;
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
    getMembersSpy = jasmine.createSpy('getMembers').and.returnValue(of([]));
    replaceMembersSpy = jasmine.createSpy('replaceMembers').and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [ProjectListComponent],
      providers: [
        provideRouter([]),
        { provide: ProjectService, useValue: { getAll: getAllSpy, delete: deleteSpy, getMembers: getMembersSpy, replaceMembers: replaceMembersSpy } },
        { provide: PositionService, useValue: { getAll: () => of([{ id: 1, name: 'Member' }]), getOrCreate: () => of({ id: 1, name: 'Member' }) } },
        { provide: UserService, useValue: { getAll: () => of([]) } },
        { provide: AuthService, useValue: fakeAuthService(currentUser) }
      ]
    }).compileComponents();
  });

  afterEach(() => {
    document.querySelectorAll('dialog[open]').forEach((dialog) => (dialog as HTMLDialogElement).close());
  });

  it('links each project card to its detail screen', () => {
    const fixture = create();

    const link = card(fixture, 'Engineering').querySelector<HTMLAnchorElement>('[data-action="open"]')!;
    expect(link.getAttribute('href')).toBe('/projects/1');
  });

  it('lists the managers of each project by name and says so when there is none', () => {
    getAllSpy.and.returnValue(
      of([
        { ...projects[0], managers: ['Maya Menon', 'Ravi Rao'] },
        { ...projects[1], managers: ['Omar Odd'] },
        projects[2]
      ])
    );
    const fixture = create();

    const names = (project: string) =>
      Array.from(card(fixture, project).querySelectorAll('[data-field="managers"] .tf-pill')).map((pill) => pill.textContent?.trim());
    expect(names('Engineering')).toEqual(['Maya Menon', 'Ravi Rao']);
    expect(card(fixture, 'Engineering').querySelector('.project__managers-label')?.textContent).toContain('Managers');
    expect(names('Finance')).toEqual(['Omar Odd']);
    expect(card(fixture, 'Finance').querySelector('.project__managers-label')?.textContent?.trim()).toBe('Manager');
    expect(card(fixture, 'Operations').querySelector('[data-field="managers"]')?.textContent).toContain('No manager yet');
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
    const link = card(fixture, 'Operations').querySelector('.project__foot a') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/employees?projectId=3');
  });

  it('lets Delete go ahead for a project with employees, warning that they are taken off it', fakeAsync(() => {
    const fixture = create();
    const toastSpy = spyOn(TestBed.inject(ToastService), 'success');
    const button = card(fixture, 'Engineering').querySelector('[data-action="delete"]') as HTMLButtonElement;

    expect(button.getAttribute('aria-disabled')).toBeNull();
    button.click();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('tf-confirm-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.textContent).toContain('12 employees will be taken off it');

    (dialog.querySelector('[data-action="confirm"]') as HTMLButtonElement).click();
    flushMicrotasks();

    expect(deleteSpy).toHaveBeenCalledWith(1);
    expect(toastSpy).toHaveBeenCalledWith('Engineering deleted');
  }));

  it('opens the team dialog from the Team button of a project', fakeAsync(() => {
    getMembersSpy.and.returnValue(of([{ userId: 4, fullName: 'Priya', email: 'priya@taskflow.com', positionId: 1, positionName: 'Member', percent: 60 }]));
    const fixture = create();

    (card(fixture, 'Engineering').querySelector('[data-action="team"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();

    expect(getMembersSpy).toHaveBeenCalledWith(1);
    const dialog = el(fixture).querySelector('app-project-team-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.querySelector('#team-dialog-title')?.textContent).toContain('Project team: Engineering');
  }));

  it('goes straight on to the Assign team step after a project is created, and it can be skipped', fakeAsync(() => {
    const fixture = create();
    const toast = spyOn(TestBed.inject(ToastService), 'success');
    fixture.componentInstance.openAssignTeam({ id: 9, name: 'Apollo' });
    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();

    const dialog = el(fixture).querySelector('app-project-team-dialog dialog') as HTMLDialogElement;
    expect(dialog.open).toBeTrue();
    expect(dialog.querySelector('#team-dialog-title')?.textContent).toContain('Assign team: Apollo');
    (dialog.querySelector('[data-action="skip"]') as HTMLButtonElement).click();
    expect(dialog.open).toBeFalse();
    expect(replaceMembersSpy).not.toHaveBeenCalled();
    expect(toast).not.toHaveBeenCalled();
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
    expect(card(fixture, 'Operations').querySelector('.project__foot a')).toBeNull();
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
