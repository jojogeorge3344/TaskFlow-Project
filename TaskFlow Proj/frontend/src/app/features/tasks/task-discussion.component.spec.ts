import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { TaskComment } from '../../core/models';
import { ToastService } from '../../shared/ui/toast.service';
import { TaskDiscussionComponent } from './task-discussion.component';
import { TaskService } from './task.service';

describe('TaskDiscussionComponent', () => {
  const comments: TaskComment[] = [
    { id: 1, body: 'Handing off the gateway work.', authorUserId: 3, authorName: 'Joe Analyst', createdAt: '2026-10-02T08:00:00Z' },
    { id: 2, body: 'Got it, will take it from here.', authorUserId: 9, authorName: 'Sara Dev', createdAt: '2026-10-03T09:30:00Z' }
  ];

  let fixture: ComponentFixture<TaskDiscussionComponent>;
  let component: TaskDiscussionComponent;
  let getCommentsSpy: jasmine.Spy;
  let addCommentSpy: jasmine.Spy;
  let toast: { success: jasmine.Spy; error: jasmine.Spy };

  beforeEach(() => {
    getCommentsSpy = jasmine.createSpy('getComments').and.returnValue(of(comments));
    addCommentSpy = jasmine.createSpy('addComment');
    toast = { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };

    TestBed.configureTestingModule({
      imports: [TaskDiscussionComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }) } },
        { provide: TaskService, useValue: { getComments: getCommentsSpy, addComment: addCommentSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });

    fixture = TestBed.createComponent(TaskDiscussionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders comments chronologically with author names', () => {
    const text = fixture.nativeElement.textContent;

    expect(text).toContain('Joe Analyst');
    expect(text).toContain('Handing off the gateway work.');
    expect(text).toContain('Sara Dev');
    expect(text).toContain('Got it, will take it from here.');
  });

  it('posts a trimmed comment and appends it to the list', () => {
    addCommentSpy.and.returnValue(
      of({ id: 3, body: 'Thanks!', authorUserId: 1, authorName: 'Boss', createdAt: '2026-10-04T08:00:00Z' })
    );

    component.body.set('  Thanks!  ');
    component.post();

    expect(addCommentSpy).toHaveBeenCalledWith(7, { body: 'Thanks!' });
    expect(component.comments().length).toBe(3);
    expect(component.body()).toBe('');
    expect(toast.success).toHaveBeenCalled();
  });

  it('ignores empty or whitespace-only comments', () => {
    component.body.set('   ');
    component.post();

    expect(addCommentSpy).not.toHaveBeenCalled();
  });

  it('toasts a failure when the comment is rejected', () => {
    addCommentSpy.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 400, statusText: 'Bad Request', error: { detail: 'Comment must not be empty.' } }))
    );

    component.body.set('Hello');
    component.post();

    expect(component.posting()).toBeFalse();
    expect(toast.error).toHaveBeenCalledWith('Comment must not be empty.');
  });

  it('shows the error state when comments cannot be loaded', () => {
    getCommentsSpy.and.returnValue(throwError(() => new HttpErrorResponse({ status: 403, statusText: 'Forbidden' })));
    TestBed.resetTestingModule();

    TestBed.configureTestingModule({
      imports: [TaskDiscussionComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }) } },
        { provide: TaskService, useValue: { getComments: getCommentsSpy, addComment: addCommentSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });

    fixture = TestBed.createComponent(TaskDiscussionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.error()).not.toBeNull();
  });

  it('render an empty state when there are no comments', () => {
    TestBed.resetTestingModule();
    getCommentsSpy.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [TaskDiscussionComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }) } },
        { provide: TaskService, useValue: { getComments: getCommentsSpy, addComment: addCommentSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });

    fixture = TestBed.createComponent(TaskDiscussionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.comments().length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('No comments yet');
  });
});