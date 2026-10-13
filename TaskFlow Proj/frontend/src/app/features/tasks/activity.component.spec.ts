import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ActivityEntry } from '../../core/models';
import { ActivityComponent } from './activity.component';
import { TaskService } from './task.service';

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

  beforeEach(() => {
    getActivitySpy = jasmine.createSpy('getActivity').and.returnValue(of(entries));

    TestBed.configureTestingModule({
      imports: [ActivityComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: TaskService, useValue: { getActivity: getActivitySpy } }
      ]
    });

    fixture = TestBed.createComponent(ActivityComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders activity entries oldest first', () => {
    const listItems = fixture.nativeElement.querySelectorAll('.activity__item');

    expect(listItems.length).toBe(2);
    expect(listItems[0].textContent).toContain('StatusChanged');
    expect(listItems[0].textContent).toContain('ToDo → InProgress');
    expect(listItems[1].textContent).toContain('CommentAdded');
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
});