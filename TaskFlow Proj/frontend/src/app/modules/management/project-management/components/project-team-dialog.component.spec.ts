import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed, fakeAsync, flushMicrotasks, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Position, ProjectMember } from '../models/project.model';
import { User } from '../../employee-management/models/user.model';
import { PositionService } from '../positions/services/position.service';
import { ProjectService } from '../services/project.service';
import { UserService } from '../../employee-management/services/user.service';
import { EMPLOYEE_SEARCH_DEBOUNCE_MS } from './employee-picker.component';
import { ToastService } from '../../../../shared/services/toast.service';
import { NO_ALLOCATIONS } from '../../../../shared/testing/users';
import { ProjectTeamDialogComponent } from './project-team-dialog.component';

const positions: Position[] = [
  { id: 1, name: 'Member' },
  { id: 3, name: 'QA' },
  { id: 4, name: 'Developer' }
];

/** Ravi and Priya hold the Manager role, so the flag may be set for them; Maya does not. */
function user(id: number, fullName: string, remainingPercent: number, isManagerRole = false): User {
  return {
    id,
    fullName,
    username: fullName.toLowerCase(),
    email: `${fullName.toLowerCase()}@taskflow.com`,
    role: isManagerRole ? 'Manager' : 'Employee',
    roleId: isManagerRole ? 2 : 3,
    isActive: true,
    createdAt: '2026-09-01T00:00:00Z',
    ...NO_ALLOCATIONS,
    totalAllocatedPercent: 100 - remainingPercent,
    remainingPercent
  };
}

const priya: ProjectMember = {
  userId: 4,
  fullName: 'Priya',
  email: 'priya@taskflow.com',
  positionId: 3,
  positionName: 'QA',
  percent: 60,
  isManager: true,
  isManagerRole: true
};

describe('ProjectTeamDialogComponent', () => {
  let fixture: ComponentFixture<ProjectTeamDialogComponent>;
  let getMembers: jasmine.Spy;
  let replaceMembers: jasmine.Spy;
  let getOrCreate: jasmine.Spy;
  let usersGetAll: jasmine.Spy;
  let successToast: jasmine.Spy;

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function dialog(): HTMLDialogElement {
    return el().querySelector('dialog') as HTMLDialogElement;
  }

  function rows(): HTMLElement[] {
    return Array.from(el().querySelectorAll<HTMLElement>('[data-row]'));
  }

  function open(afterCreate = false): void {
    fixture.componentInstance.open({ id: 9, name: 'Apollo' }, afterCreate);
    fixture.detectChanges();
    flushMicrotasks();
    fixture.detectChanges();
  }

  function addRow(): void {
    (el().querySelector('[data-action="add-member"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function pickEmployee(row: number, name: string): void {
    const input = rows()[row].querySelector('app-employee-picker input') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    tick(EMPLOYEE_SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();
    const option = Array.from(rows()[row].querySelectorAll<HTMLButtonElement>('.employee-picker__option')).find((o) =>
      (o.textContent ?? '').includes(name)
    )!;
    option.click();
    fixture.detectChanges();
  }

  function setPercent(row: number, value: string): void {
    const input = rows()[row].querySelector('.team-row__percent') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function setPosition(row: number, name: string): void {
    const input = rows()[row].querySelector('app-position-combobox input') as HTMLInputElement;
    input.value = name;
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function managerBox(row: number): HTMLInputElement {
    return rows()[row].querySelector('[data-field="is-manager"]') as HTMLInputElement;
  }

  function setManager(row: number, checked: boolean): void {
    const box = managerBox(row);
    box.checked = checked;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  function saveButton(): HTMLButtonElement {
    return el().querySelector('[data-action="save-team"]') as HTMLButtonElement;
  }

  function save(): void {
    (el().querySelector('[data-action="save-team"]') as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  beforeEach(async () => {
    getMembers = jasmine.createSpy('getMembers').and.returnValue(of([]));
    replaceMembers = jasmine.createSpy('replaceMembers').and.returnValue(of([]));
    getOrCreate = jasmine.createSpy('getOrCreate').and.callFake((name: string) => of({ id: 77, name }));
    usersGetAll = jasmine.createSpy('getAll').and.returnValue(of([user(5, 'Ravi', 40, true), user(6, 'Maya', 0)]));

    await TestBed.configureTestingModule({
      imports: [ProjectTeamDialogComponent],
      providers: [
        { provide: ProjectService, useValue: { getMembers, replaceMembers } },
        { provide: PositionService, useValue: { getAll: () => of(positions), getOrCreate } },
        { provide: UserService, useValue: { getAll: usersGetAll } }
      ]
    }).compileComponents();

    successToast = spyOn(TestBed.inject(ToastService), 'success');
    fixture = TestBed.createComponent(ProjectTeamDialogComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    document.querySelectorAll('dialog[open]').forEach((d) => (d as HTMLDialogElement).close());
  });

  it('offers a skippable Assign team step right after a project is created', fakeAsync(() => {
    open(true);

    expect(dialog().open).toBeTrue();
    expect(el().querySelector('#team-dialog-title')?.textContent).toContain('Assign team: Apollo');
    expect(el().querySelector('[data-action="skip"]')?.textContent).toContain('Skip for now');
    expect(el().querySelector('[data-empty]')).not.toBeNull();

    (el().querySelector('[data-action="skip"]') as HTMLButtonElement).click();
    expect(dialog().open).toBeFalse();
    expect(replaceMembers).not.toHaveBeenCalled();
  }));

  it('shows the current members with their position and percentage when reopened later', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    open(false);

    expect(getMembers).toHaveBeenCalledWith(9);
    expect(el().querySelector('#team-dialog-title')?.textContent).toContain('Project team: Apollo');
    expect(rows().length).toBe(1);
    expect((rows()[0].querySelector('app-employee-picker input') as HTMLInputElement).value).toBe('Priya');
    expect((rows()[0].querySelector('app-position-combobox input') as HTMLInputElement).value).toBe('QA');
    expect((rows()[0].querySelector('.team-row__percent') as HTMLInputElement).value).toBe('60');
  }));

  it('shows column headings above the rows only when there are rows', fakeAsync(() => {
    open(true);
    expect(el().querySelector('[data-team-head]')).toBeNull();

    addRow();

    const headings = Array.from(el().querySelectorAll('[data-team-head] span')).map((s) => (s.textContent ?? '').trim());
    expect(headings).toEqual(['Employee', 'Role', 'Allocation %', 'Project manager', '']);
  }));

  it('lays the headings and the rows out on the same column template', fakeAsync(() => {
    open(true);
    addRow();

    const head = getComputedStyle(el().querySelector('[data-team-head]') as HTMLElement);
    const row = getComputedStyle(rows()[0]);
    expect(head.gridTemplateColumns).toBe(row.gridTemplateColumns);
    expect(head.textTransform).toBe('none');
    expect(getComputedStyle(el().querySelector('[data-team-head] span') as HTMLElement).whiteSpace).toBe('nowrap');
  }));

  it('labels every field of a row so the narrow layout can show the label', fakeAsync(() => {
    open(true);
    addRow();

    const labels = Array.from(rows()[0].querySelectorAll('.team-cell')).map((cell) => cell.getAttribute('data-label'));
    expect(labels).toEqual(['Employee', 'Role', 'Allocation %']);
    expect(rows()[0].querySelector('.team-row__manager')?.textContent).toContain('Manager');
  }));

  it('shows the manager checkbox without a repeated "Manager" word beside it', fakeAsync(() => {
    open(true);
    addRow();

    const text = rows()[0].querySelector('.team-row__manager-text') as HTMLElement;
    expect(getComputedStyle(text).display).toBe('none');
    expect(managerBox(0).getAttribute('aria-label')).toBe('Project manager: employee 1');
    expect(rows()[0].querySelector('.team-row__manager')?.getAttribute('title')).toBeTruthy();
  }));

  it('starts a new row with no role and a "Role in this project" placeholder', fakeAsync(() => {
    open(true);
    addRow();

    const input = rows()[0].querySelector('app-position-combobox input') as HTMLInputElement;
    expect(input.value).toBe('');
    expect(input.placeholder).toBe('Role in this project');
  }));

  it('searches employees on the server and shows how much of their time is free', fakeAsync(() => {
    open(true);
    addRow();
    const input = rows()[0].querySelector('app-employee-picker input') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    input.value = 'rav';
    input.dispatchEvent(new Event('input'));
    tick(EMPLOYEE_SEARCH_DEBOUNCE_MS);
    fixture.detectChanges();

    expect(usersGetAll).toHaveBeenCalledWith({ search: 'rav', isActive: true });
    const options = Array.from(rows()[0].querySelectorAll('.employee-picker__option')).map((o) => (o.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(options).toEqual(['Ravi 40% remaining', 'Maya Fully allocated']);
  }));

  it('saves several people, each with an employee, a position and a percentage', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    open(false);
    addRow();
    pickEmployee(1, 'Ravi');
    setPosition(1, 'Developer');
    setPercent(1, '30');

    save();

    expect(replaceMembers).toHaveBeenCalledOnceWith(9, [
      { userId: 4, positionId: 3, percent: 60, isManager: true },
      { userId: 5, positionId: 4, percent: 30, isManager: false }
    ]);
    expect(successToast).toHaveBeenCalledWith('Team saved');
    expect(dialog().open).toBeFalse();
  }));

  it('creates a position typed by the assigner and uses it', fakeAsync(() => {
    open(true);
    addRow();
    pickEmployee(0, 'Ravi');
    setPosition(0, 'Scrum Master');
    setPercent(0, '25');
    setManager(0, true);

    save();

    expect(getOrCreate).toHaveBeenCalledWith('Scrum Master');
    expect(replaceMembers).toHaveBeenCalledOnceWith(9, [{ userId: 5, positionId: 77, percent: 25, isManager: true }]);
  }));

  it('starts a picked employee on their remaining percentage and flags more than they have left', fakeAsync(() => {
    open(true);
    addRow();
    pickEmployee(0, 'Ravi');
    expect((rows()[0].querySelector('.team-row__percent') as HTMLInputElement).value).toBe('40');

    setPercent(0, '55');

    expect(rows()[0].querySelector('[data-row-error]')?.textContent).toContain('Ravi has only 40% remaining.');
    expect((el().querySelector('[data-action="save-team"]') as HTMLButtonElement).disabled).toBeTrue();
    save();
    expect(replaceMembers).not.toHaveBeenCalled();
  }));

  it('refuses a percentage that is not a whole number from 1 to 100', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    open(false);

    setPercent(0, '101');
    expect(rows()[0].querySelector('[data-row-error]')?.textContent).toContain('whole percentage from 1 to 100');
    setPercent(0, '2.5');
    expect((el().querySelector('[data-action="save-team"]') as HTMLButtonElement).disabled).toBeTrue();
  }));

  it('does not save an unfinished row', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    open(false);
    addRow();

    save();

    expect(replaceMembers).not.toHaveBeenCalled();
    expect(el().querySelector('[data-team-error]')?.textContent).toContain('employee, a position and a percentage');
  }));

  it('shows the server message and saves nothing when someone would pass 100 percent', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    replaceMembers.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { error: 'Priya would be allocated 120% in total. They already have 60% on other projects, so 40% remains.' }
          })
      )
    );
    open(false);

    save();

    expect(el().querySelector('[data-team-error]')?.textContent).toContain('Priya would be allocated 120% in total');
    expect(dialog().open).toBeTrue();
    expect(successToast).not.toHaveBeenCalled();
  }));

  it('removes a member and saves the smaller team', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    open(false);

    (rows()[0].querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();
    save();

    expect(replaceMembers).toHaveBeenCalledOnceWith(9, []);
    expect(successToast).toHaveBeenCalledWith('Team cleared');
  }));

  it('needs a manager before a team with people can be saved', fakeAsync(() => {
    open(true);
    addRow();
    pickEmployee(0, 'Ravi');
    setPercent(0, '25');

    expect(el().querySelector('[data-manager-error]')?.textContent).toContain('Mark at least one person as the project manager');
    expect(saveButton().disabled).toBeTrue();
    save();
    expect(replaceMembers).not.toHaveBeenCalled();

    setManager(0, true);

    expect(el().querySelector('[data-manager-error]')).toBeNull();
    expect(saveButton().disabled).toBeFalse();
  }));

  it('lets an empty team be saved without a manager', fakeAsync(() => {
    open(true);

    expect(el().querySelector('[data-manager-error]')).toBeNull();
    expect(saveButton().disabled).toBeFalse();
  }));

  it('allows several managers on one team', fakeAsync(() => {
    getMembers.and.returnValue(of([priya]));
    open(false);
    addRow();
    pickEmployee(1, 'Ravi');
    setPercent(1, '30');
    setPosition(1, 'Member');
    setManager(1, true);

    save();

    expect(replaceMembers).toHaveBeenCalledOnceWith(9, [
      { userId: 4, positionId: 3, percent: 60, isManager: true },
      { userId: 5, positionId: 1, percent: 30, isManager: true }
    ]);
  }));

  it('disables the manager flag for a person without the Manager role and clears it on a swap', fakeAsync(() => {
    open(true);
    addRow();
    pickEmployee(0, 'Maya');

    expect(managerBox(0).disabled).toBeTrue();
    expect(managerBox(0).closest('label')?.getAttribute('title')).toContain('Only a person with the Manager role');

    pickEmployee(0, 'Ravi');
    expect(managerBox(0).disabled).toBeFalse();
    setManager(0, true);
    pickEmployee(0, 'Maya');

    expect(managerBox(0).checked).toBeFalse();
    expect(managerBox(0).disabled).toBeTrue();
  }));

  it('pre-fills the manager flag of the current team and disables it for non-managers', fakeAsync(() => {
    getMembers.and.returnValue(of([priya, { ...priya, userId: 6, fullName: 'Maya', isManager: false, isManagerRole: false }]));
    open(false);

    expect(managerBox(0).checked).toBeTrue();
    expect(managerBox(1).checked).toBeFalse();
    expect(managerBox(1).disabled).toBeTrue();
  }));

  it('shows an error and no editor when the team cannot be loaded', fakeAsync(() => {
    getMembers.and.returnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    open(false);

    expect(el().querySelector('.tf-alert')?.textContent).toContain('Could not load the team');
    expect(el().querySelector('[data-action="add-member"]')).toBeNull();
  }));
});
