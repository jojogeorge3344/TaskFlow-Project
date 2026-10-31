import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../../shared/auth/auth.service';
import { TaskComment, TaskDetails } from '../../models/task.model';
import { ToastService } from '../../../../../shared/services/toast.service';
import { TaskDiscussionComponent } from './task-discussion.component';
import { TaskService } from '../../services/task.service';

describe('TaskDiscussionComponent', () => {
  const taskStub = {
    id: 7,
    taskCode: 'TSK-1007',
    title: 'Integrate payments',
    assignedToUserId: 3,
    assignedToName: 'Joe Analyst',
    status: 'InProgress',
    priority: 'High',
    deadline: '2026-10-20T10:00:00Z',
    recipients: [
      { userId: 3, fullName: 'Joe Analyst' },
      { userId: 9, fullName: 'Sara Dev' }
    ]
  } as unknown as TaskDetails;

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
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }), hasPermission: () => true } },
        { provide: TaskService, useValue: { getById: () => of(taskStub), getComments: getCommentsSpy, addComment: addCommentSpy } },
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

  it('groups comments by day and shows who is notified', () => {
    const text = fixture.nativeElement.textContent;
    expect(fixture.nativeElement.querySelectorAll('.discussion__day').length).toBe(2);
    expect(fixture.nativeElement.querySelector('[data-comment-count]').textContent).toContain('2 comments');

    const notified = fixture.nativeElement.querySelector('[data-notified]').textContent;
    expect(notified).toContain('Joe Analyst');
    expect(notified).toContain('Assignee');
    expect(notified).toContain('Sara Dev');
    expect(text).toContain('TSK-1007');
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

  it('posts through the form itself instead of submitting the page natively', () => {
    addCommentSpy.and.returnValue(
      of({ id: 3, body: 'From the form', authorUserId: 1, authorName: 'Boss', createdAt: '2026-10-04T08:00:00Z' })
    );
    const textarea: HTMLTextAreaElement = fixture.nativeElement.querySelector('#discussion-body');
    textarea.value = 'From the form';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const submit: HTMLButtonElement = fixture.nativeElement.querySelector('form button[type="submit"]');
    submit.click();
    fixture.detectChanges();

    expect(addCommentSpy).toHaveBeenCalledWith(7, { body: 'From the form' });
    expect(fixture.nativeElement.textContent).toContain('From the form');
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
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }), hasPermission: () => true } },
        { provide: TaskService, useValue: { getById: () => of(taskStub), getComments: getCommentsSpy, addComment: addCommentSpy } },
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
        { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }), hasPermission: () => true } },
        { provide: TaskService, useValue: { getById: () => of(taskStub), getComments: getCommentsSpy, addComment: addCommentSpy } },
        { provide: ToastService, useValue: toast }
      ]
    });

    fixture = TestBed.createComponent(TaskDiscussionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.comments().length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('No comments yet');
  });

  describe('own comments', () => {
    const mine: TaskComment = { id: 5, body: 'My note', authorUserId: 1, authorName: 'Boss', createdAt: '2026-10-05T08:00:00Z' };
    let updateCommentSpy: jasmine.Spy;
    let deleteCommentSpy: jasmine.Spy;

    function create(allowed: string[] = ['tasks.comment.edit', 'tasks.comment.delete']): void {
      TestBed.resetTestingModule();
      getCommentsSpy.and.returnValue(of([...comments, mine]));
      updateCommentSpy = jasmine.createSpy('updateComment');
      deleteCommentSpy = jasmine.createSpy('deleteComment');
      TestBed.configureTestingModule({
        imports: [TaskDiscussionComponent],
        providers: [
          { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '7' } } } },
          { provide: AuthService, useValue: { currentUser: () => ({ id: 1, fullName: 'Boss', role: 'Manager' }), hasPermission: (key: string) => allowed.includes(key) } },
          {
            provide: TaskService,
            useValue: { getById: () => of(taskStub), getComments: getCommentsSpy, addComment: addCommentSpy, updateComment: updateCommentSpy, deleteComment: deleteCommentSpy }
          },
          { provide: ToastService, useValue: toast }
        ]
      });
      fixture = TestBed.createComponent(TaskDiscussionComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    }

    it('shows edit and delete only on the current user\'s own comment', () => {
      create();

      expect(fixture.nativeElement.querySelectorAll('[data-action="edit"]').length).toBe(1);
      expect(fixture.nativeElement.querySelectorAll('[data-action="delete"]').length).toBe(1);
    });

    it('hides edit and delete without the permissions', () => {
      create([]);

      expect(fixture.nativeElement.querySelector('[data-action="edit"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-action="delete"]')).toBeNull();
    });

    it('saves an edited comment and marks it edited', () => {
      create();
      updateCommentSpy.and.returnValue(of({ ...mine, body: 'Revised', editedAt: '2026-10-06T08:00:00Z' }));

      component.startEdit(mine);
      component.editBody.set('  Revised  ');
      component.saveEdit(mine);
      fixture.detectChanges();

      expect(updateCommentSpy).toHaveBeenCalledWith(7, 5, { body: 'Revised' });
      expect(component.editingId()).toBeNull();
      expect(fixture.nativeElement.textContent).toContain('Revised');
      expect(fixture.nativeElement.querySelector('[data-edited]')).not.toBeNull();
    });

    it('does not save an empty edit', () => {
      create();

      component.startEdit(mine);
      component.editBody.set('   ');
      component.saveEdit(mine);

      expect(updateCommentSpy).not.toHaveBeenCalled();
    });

    it('deletes a comment after confirmation and removes it from the list', async () => {
      create();
      deleteCommentSpy.and.returnValue(of(void 0));
      spyOn(component['confirmDialog'](), 'open').and.resolveTo(true);

      await component.remove(mine);
      fixture.detectChanges();

      expect(deleteCommentSpy).toHaveBeenCalledWith(7, 5);
      expect(component.comments().some((comment) => comment.id === 5)).toBeFalse();
      expect(fixture.nativeElement.textContent).not.toContain('My note');
    });

    it('keeps the comment when the delete is not confirmed', async () => {
      create();
      spyOn(component['confirmDialog'](), 'open').and.resolveTo(false);

      await component.remove(mine);

      expect(deleteCommentSpy).not.toHaveBeenCalled();
      expect(component.comments().some((comment) => comment.id === 5)).toBeTrue();
    });
  });
});
