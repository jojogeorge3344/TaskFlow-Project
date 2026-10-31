import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { Project, ProjectMember, ProjectTask } from '../../core/models';
import { ProjectService } from '../../core/services/project.service';
import { ProjectDetailComponent } from './project-detail.component';

describe('ProjectDetailComponent', () => {
  const project: Project = { id: 7, name: 'Apollo', description: 'Moon landing', createdAt: '2026-09-01T00:00:00Z' };

  function member(overrides: Partial<ProjectMember>): ProjectMember {
    return {
      userId: 1,
      fullName: 'Ravi Kumar',
      email: 'ravi@taskflow.com',
      positionId: 4,
      positionName: 'Developer',
      percent: 60,
      isManager: false,
      isManagerRole: false,
      ...overrides
    };
  }

  function task(overrides: Partial<ProjectTask>): ProjectTask {
    return {
      id: 1,
      taskCode: 'TSK-000001',
      title: 'Build login',
      status: 'InProgress',
      priority: 'High',
      startDate: '2026-10-01T00:00:00Z',
      deadline: '2099-10-10T00:00:00Z',
      assignedToUserId: 1,
      assignedToName: 'Ravi Kumar',
      assigneePositionId: 4,
      assigneePositionName: 'Developer',
      ...overrides
    };
  }

  const members = [
    member({}),
    member({ userId: 2, fullName: 'Priya Nair', positionId: 3, positionName: 'QA', percent: 40 }),
    member({ userId: 3, fullName: 'Dev Two', positionId: 4, positionName: 'Developer', percent: 20, isManager: true })
  ];
  const tasks = [task({}), task({ id: 2, taskCode: 'TSK-000002', title: 'Old bug', deadline: '2020-01-01T00:00:00Z' })];

  let getById: jasmine.Spy;
  let getMembers: jasmine.Spy;
  let getTasks: jasmine.Spy;

  function create(): ComponentFixture<ProjectDetailComponent> {
    TestBed.configureTestingModule({
      imports: [ProjectDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '7' }) } } },
        { provide: ProjectService, useValue: { getById, getMembers, getTasks } }
      ]
    });
    const fixture = TestBed.createComponent(ProjectDetailComponent);
    fixture.detectChanges();
    return fixture;
  }

  function el(fixture: ComponentFixture<ProjectDetailComponent>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function choose(fixture: ComponentFixture<ProjectDetailComponent>, filter: string, value: string): void {
    const select = el(fixture).querySelector(`[data-filter="${filter}"]`) as HTMLSelectElement;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    getById = jasmine.createSpy('getById').and.returnValue(of(project));
    getMembers = jasmine.createSpy('getMembers').and.returnValue(of(members));
    getTasks = jasmine.createSpy('getTasks').and.returnValue(of(tasks));
  });

  it('shows the project, its team with roles and allocation, and its tasks', () => {
    const fixture = create();

    expect(el(fixture).querySelector('[data-field="name"]')?.textContent).toContain('Apollo');
    const team = Array.from(el(fixture).querySelectorAll('[data-member]')).map((m) => (m.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(team[0]).toContain('Ravi Kumar');
    expect(team[0]).toContain('Developer');
    expect(team[0]).toContain('60%');
    // The project manager gets no badge here: the team dialog is where the flag is shown and set.
    expect(team[2]).toContain('Dev Two');
    expect(team[2]).not.toContain('Manager');
    expect(el(fixture).querySelectorAll('[data-task]').length).toBe(2);
    expect(getTasks).toHaveBeenCalledOnceWith(7);
  });

  it('links each task with a return path back to this project', () => {
    const fixture = create();

    const href = el(fixture).querySelector('[data-task] a.pd__task-title')?.getAttribute('href') ?? '';

    expect(href).toContain('returnUrl=%2Fprojects%2F7');
  });

  it('flags overdue tasks and counts them in the summary', () => {
    const fixture = create();

    expect(fixture.componentInstance.stats().overdue).toBe(1);
    expect(el(fixture).querySelectorAll('.pd__overdue').length).toBe(1);
  });

  it('offers only the roles held on this project, once each', () => {
    const fixture = create();

    const options = Array.from(el(fixture).querySelectorAll('[data-filter="role"] option')).map((o) => o.textContent?.trim());
    expect(options).toEqual(['All roles', 'Developer', 'QA']);
  });

  it('asks the server to filter by role and status together, and clears them again', () => {
    const fixture = create();
    getTasks.calls.reset();
    getTasks.and.returnValue(of([tasks[0]]));

    choose(fixture, 'role', '3');
    choose(fixture, 'status', 'Blocked');

    expect(getTasks.calls.mostRecent().args).toEqual([
      7,
      { status: 'Blocked', priority: null, assignedToUserId: null, positionId: 3, deadlineFrom: null, deadlineTo: null }
    ]);
    expect(el(fixture).querySelectorAll('[data-task]').length).toBe(1);

    (el(fixture).querySelector('[data-action="clear-filters"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el(fixture).querySelectorAll('[data-task]').length).toBe(2);
    expect(fixture.componentInstance.filtersActive()).toBeFalse();
  });

  it('shows a no-match state with a way out when the filters match nothing', () => {
    const fixture = create();
    getTasks.and.returnValue(of([]));

    choose(fixture, 'priority', 'Low');

    expect(el(fixture).querySelector('[data-empty="no-match"]')).not.toBeNull();
  });

  it('shows empty states for a project with no team and no tasks', () => {
    getMembers.and.returnValue(of([]));
    getTasks.and.returnValue(of([]));
    const fixture = create();

    expect(el(fixture).querySelector('[data-empty="team"]')).not.toBeNull();
    expect(el(fixture).querySelector('[data-empty="tasks"]')).not.toBeNull();
  });

  it('shows "Project not found" for an unknown project', () => {
    getById.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    const fixture = create();

    expect(el(fixture).querySelector('[data-state="not-found"]')).not.toBeNull();
  });
});
