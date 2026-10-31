import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../../shared/auth/auth.service';
import { ActivityEntry } from '../../models/task.model';
import { ToastService } from '../../../../../shared/services/toast.service';
import { ActivityComponent } from './activity.component';
import { TaskService } from '../../services/task.service';

describe('ActivityComponent', () => {
  const entries: ActivityEntry[] = [
    {
      id: 1,
      eventType: 'StatusChanged',
      actorUserId: 3,
      actorName: 'Joe Analyst',
      oldValue: 'ToDo',
      newValue: 'InProgress',
      message: 'Status changed from ToDo to InProgress',
      createdAt: '2026-10-03T08:00:00Z'
    },
    {
      id: 2,
      eventType: 'CommentAdded',
      actorUserId: 9,
      actorName: 'Sara Dev',
      oldValue: null,
      newValue: null,
      message: 'New comment added',
      createdAt: '2026-10-04T09:00:00Z'
    }
  ];

  let fixture: ComponentFixture<ActivityComponent>;
  let component: ActivityComponent;
  let getActivitySpy: jasmine.Spy;
  let queryEventType: string | null;
  let currentRole: string;
  let updateHoursSpy: jasmine.Spy;
  let deleteHoursSpy: jasmine.Spy;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };

  beforeEach(() => {
    queryEventType = null;
    currentRole = 'Administrator';
    updateHoursSpy = jasmine.createSpy('updateHoursEntry');
    deleteHoursSpy = jasmine.createSpy('deleteHoursEntry');
    toast = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
    getActivitySpy = jasmine.createSpy('getActivity').and.returnValue(of(entries));

    TestBed.configureTestingModule({
      imports: [ActivityComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' }, queryParamMap: { get: (key: string) => (key === 'eventType' ? queryEventType : null) } } } },
        { provide: TaskService, useValue: { getActivity: getActivitySpy, updateHoursEntry: updateHoursSpy, deleteHoursEntry: deleteHoursSpy } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: currentRole }) } },
        { provide: ToastService, useValue: toast }
      ]
    });

    fixture = TestBed.createComponent(ActivityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('applies the eventType query parameter as the initial filter', () => {
    TestBed.resetTestingModule();
    queryEventType = 'HoursLogged';
    TestBed.configureTestingModule({
      imports: [ActivityComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' }, queryParamMap: { get: (key: string) => (key === 'eventType' ? queryEventType : null) } } } },
        { provide: TaskService, useValue: { getActivity: getActivitySpy, updateHoursEntry: updateHoursSpy, deleteHoursEntry: deleteHoursSpy } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: currentRole }) } },
        { provide: ToastService, useValue: toast }
      ]
    });
    const filtered = TestBed.createComponent(ActivityComponent);
    filtered.detectChanges();

    expect(filtered.componentInstance.eventType()).toBe('HoursLogged');
    expect(getActivitySpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ eventType: 'HoursLogged' }));
  });

  it('renders activity entries newest first', () => {
    const listItems = fixture.nativeElement.querySelectorAll('.activity__item');

    expect(listItems.length).toBe(2);
    expect(listItems[0].textContent).toContain('Comment added');
    expect(listItems[1].textContent).toContain('Status changed');
    const diff = listItems[1].querySelector('[data-diff]').textContent;
    expect(diff).toContain('ToDo');
    expect(diff).toContain('InProgress');
  });

  it('applies the filters when the form is submitted', () => {
    component.eventType.set('StatusChanged');
    component.search.set('to InProgress');
    component.from.set('2026-10-01');
    component.to.set('2026-10-31');

    fixture.componentInstance.load();

    expect(getActivitySpy).toHaveBeenCalledWith(7, {
      eventType: 'StatusChanged',
      search: 'to InProgress',
      from: '2026-10-01',
      to: '2026-10-31'
    });
  });

  it('clears the filters and reloads', () => {
    component.eventType.set('Reassigned');
    component.search.set('x');
    component.clearFilters();

    expect(component.eventType()).toBe('');
    expect(component.search()).toBe('');
    expect(getActivitySpy).toHaveBeenCalledWith(
      7,
      jasmine.objectContaining({ eventType: undefined, search: undefined })
    );
  });

  it('shows an empty state when there is no matching activity', () => {
    getActivitySpy.and.returnValue(of([]));
    component.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No activity found');
  });

  it('shows the error state when the log cannot be loaded', () => {
    getActivitySpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' })));
    component.load();
    fixture.detectChanges();

    expect(component.error()).not.toBeNull();
  });

  it('lists comment events as filter options and shows the comment text', () => {
    expect(component.eventTypes()).toEqual(jasmine.arrayContaining(['CommentAdded', 'CommentEdited', 'CommentDeleted']));

    const base = { id: 9, eventType: 'CommentAdded', actorUserId: 1, actorName: 'Boss', message: 'New comment added', createdAt: '2026-10-05T08:00:00Z' };
    expect(component.changeLabel({ ...base, oldValue: null, newValue: 'Looks good' })).toBe('Looks good');
    expect(component.changeLabel({ ...base, oldValue: 'Old text', newValue: 'New text' })).toBe('Old text → New text');
    expect(component.changeLabel({ ...base, oldValue: 'Removed text', newValue: null })).toBe('Removed text');
  });

  describe('tracked hours entries', () => {
    const hoursEntry: ActivityEntry = {
      id: 5,
      eventType: 'HoursLogged',
      actorUserId: 1,
      actorName: 'Boss',
      message: 'Worked hours logged: 5h (total)',
      oldValue: '2',
      newValue: '5',
      createdAt: '2026-10-04T09:00:00Z'
    };

    function open(role: string): void {
      TestBed.resetTestingModule();
      currentRole = role;
      getActivitySpy.and.returnValue(of([hoursEntry, ...entries]));
      getActivitySpy.calls.reset();
      TestBed.configureTestingModule({
        imports: [ActivityComponent],
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' }, queryParamMap: { get: () => null } } } },
          { provide: TaskService, useValue: { getActivity: getActivitySpy, updateHoursEntry: updateHoursSpy, deleteHoursEntry: deleteHoursSpy } },
          { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: currentRole }) } },
          { provide: ToastService, useValue: toast }
        ]
      });
      fixture = TestBed.createComponent(ActivityComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    }

    it('shows edit and delete on hours entries only to the Administrator', () => {
      open('Administrator');
      expect(fixture.nativeElement.querySelector('[data-action="edit-hours"]')).not.toBeNull();
      expect(fixture.nativeElement.querySelectorAll('[data-action="delete-hours"]').length).toBe(1);

      open('Manager');
      expect(fixture.nativeElement.querySelector('[data-action="edit-hours"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-action="delete-hours"]')).toBeNull();
    });

    it('works out the hours an entry added', () => {
      open('Administrator');
      expect(component.entryHours(hoursEntry)).toBe(3);
    });

    it('saves corrected hours and reloads the history', () => {
      open('Administrator');
      updateHoursSpy.and.returnValue(of(void 0));

      component.startEditHours(hoursEntry);
      component.editHours.set('4');
      component.saveHours(hoursEntry);

      expect(updateHoursSpy).toHaveBeenCalledWith(7, 5, 4);
      expect(getActivitySpy.calls.count()).toBe(2);
      expect(component.editingId()).toBeNull();
    });

    it('rejects non-positive hours without calling the API', () => {
      open('Administrator');

      component.editHours.set('0');
      component.saveHours(hoursEntry);

      expect(updateHoursSpy).not.toHaveBeenCalled();
      expect(toast.error).toHaveBeenCalled();
    });

    it('deletes an entry after confirmation and reloads', async () => {
      open('Administrator');
      deleteHoursSpy.and.returnValue(of(void 0));
      spyOn(component['confirmDialog'](), 'open').and.resolveTo(true);

      await component.deleteHours(hoursEntry);

      expect(deleteHoursSpy).toHaveBeenCalledWith(7, 5);
      expect(getActivitySpy.calls.count()).toBe(2);
    });

    it('keeps the entry when the delete is not confirmed', async () => {
      open('Administrator');
      spyOn(component['confirmDialog'](), 'open').and.resolveTo(false);

      await component.deleteHours(hoursEntry);

      expect(deleteHoursSpy).not.toHaveBeenCalled();
    });
  });


  it('filters with the quick pills and toggles back to all events', () => {
    getActivitySpy.calls.reset();

    (fixture.nativeElement.querySelector('[data-pill="HoursLogged"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(getActivitySpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ eventType: 'HoursLogged' }));
    expect(fixture.nativeElement.querySelector('[data-pill="HoursLogged"]').getAttribute('aria-pressed')).toBe('true');

    (fixture.nativeElement.querySelector('[data-pill="HoursLogged"]') as HTMLButtonElement).click();
    expect(component.eventType()).toBe('');

    component.selectOther('Created');
    expect(getActivitySpy).toHaveBeenCalledWith(7, jasmine.objectContaining({ eventType: 'Created' }));
    expect(component.otherTypes()).not.toContain('HoursLogged');
  });

  it('groups entries by day and shows the event count', () => {
    expect(component.dayGroups().length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('.activity__day').length).toBe(2);
    expect(fixture.nativeElement.querySelector('[data-event-count]').textContent).toContain('2 events');
    expect(component.eventMeta('CommentEdited').label).toBe('Comment edited');
    expect(component.eventMeta('Unknown').tone).toBe('gray');
  });

  it('lists the newest entry first', () => {
    const older = { ...entries[0], id: 1, createdAt: '2026-10-01T08:00:00Z' };
    const newer = { ...entries[0], id: 2, createdAt: '2026-10-03T08:00:00Z' };
    getActivitySpy.and.returnValue(of([older, newer]));

    component.load();

    expect(component.entries().map((entry) => entry.id)).toEqual([2, 1]);
  });
});
