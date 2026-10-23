import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivityEntry,
  CreateTaskRequest,
  PagedResult,
  TaskCard,
  TaskListItem,
  TaskComment,
  TaskDetails,
  TaskRecipient,
  UpdateTaskRequest
} from '../../core/models';
import { TaskService } from './task.service';

describe('TaskService', () => {
  let service: TaskService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TaskService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists a page of tasks with filter query params', () => {
    const page: PagedResult<TaskListItem> = {
      items: [
        {
          id: 1,
          taskCode: 'TSK-000001',
          title: 'Design mockups',
          assignedToUserId: 3,
          assignedToName: 'Joe',
          createdByUserId: 1,
          createdByName: 'Boss',
          priority: 'High',
          startDate: '2026-10-10T08:00:00Z',
          deadline: '2026-10-20T10:00:00Z',
          estimatedHours: 6,
          status: 'InProgress',
          stage: 'Upcoming',
          isOverdue: false,
          projectId: 4,
          projectName: 'Apollo'
        }
      ],
      total: 1,
      page: 2,
      pageSize: 10
    };
    let result: PagedResult<TaskListItem> | undefined;

    service
      .list({ search: 'design', assignedToUserId: 3, status: 'InProgress', priority: null, deadlineFrom: '', page: 2, pageSize: 10 })
      .subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks?search=design&assignedToUserId=3&status=InProgress&page=2&pageSize=10');
    expect(request.request.method).toBe('GET');
    request.flush(page);
    expect(result).toEqual(page);
  });

  it('lists tasks without params when no filter is given', () => {
    service.list().subscribe();

    const request = http.expectOne('/api/v1/tasks');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({ items: [], total: 0, page: 1, pageSize: 20 });
  });

  it('loads the assignee options', () => {
    let result: TaskRecipient[] | undefined;

    service.getAssignees().subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks/assignees');
    expect(request.request.method).toBe('GET');
    request.flush([{ userId: 3, fullName: 'Joe' }]);
    expect(result).toEqual([{ userId: 3, fullName: 'Joe' }]);
  });

  it('creates a task', () => {
    const body: CreateTaskRequest = {
      title: 'Plan launch',
      description: null,
      projectId: 5,
      assignedToUserId: 3,
      priority: 'Medium',
      startDate: '2026-10-14T00:00:00.000Z',
      deadline: '2026-10-18T00:00:00.000Z',
      estimatedHours: 4,
      additionalRecipientUserIds: [9],
      firstReminderDays: 2,
      secondReminderDays: 1,
      overdueReminderFrequency: 'Daily'
    };
    let createdId: number | undefined;

    service.create(body).subscribe((value) => (createdId = value.id));

    const request = http.expectOne('/api/v1/tasks');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({ id: 42 }, { status: 201, statusText: 'Created' });
    expect(createdId).toBe(42);
  });

  it('loads the board list', () => {
    const cards: TaskCard[] = [
      {
        id: 1,
        taskCode: 'TSK-1001',
        title: 'Design mockups',
        assignedToUserId: 3,
        assignedToName: 'Joe',
        priority: 'High',
        deadline: '2026-10-20T10:00:00Z',
        status: 'InProgress',
        isOverdue: true
      }
    ];
    let result: TaskCard[] | undefined;

    service.getBoard().subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks/board');
    expect(request.request.method).toBe('GET');
    request.flush(cards);
    expect(result).toEqual(cards);
  });

  it('loads task details', () => {
    const details: TaskDetails = {
      id: 7,
      taskCode: 'TSK-1007',
      title: 'Write report',
      description: null,
      assignedToUserId: 3,
      assignedToName: 'Joe',
      createdByUserId: 1,
      createdByName: 'Boss',
      priority: 'Medium',
      startDate: '2026-10-01T08:00:00Z',
      deadline: '2026-10-20T10:00:00Z',
      estimatedHours: 8,
      status: 'ToDo',
      stage: 'Upcoming',
      recipients: [],
      createdAt: '2026-10-01T08:00:00Z',
      updatedAt: '2026-10-01T08:00:00Z',
      projectId: 4,
      projectName: 'Apollo',
      firstReminderDays: 2,
      secondReminderDays: 1,
      overdueReminderFrequency: 'Daily'
    };
    let result: TaskDetails | undefined;

    service.getById(7).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks/7');
    expect(request.request.method).toBe('GET');
    request.flush(details);
    expect(result).toEqual(details);
  });

  it('updates a task', () => {
    let result: TaskDetails | undefined;
    const requestBody: UpdateTaskRequest = {
      title: 'After update',
      description: 'Updated body',
      priority: 'High',
      startDate: '2026-10-01T08:00:00Z',
      deadline: '2026-10-25T10:00:00Z',
      estimatedHours: 12
    };

    service.update(7, requestBody).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks/7');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(requestBody);
    request.flush({ ...requestBody, id: 7, taskCode: 'TSK-1007', status: 'ToDo', stage: 'Upcoming', recipients: [] });
    expect(result?.title).toBe('After update');
  });

  it('changes a task status', () => {
    let status: string | undefined;

    service.changeStatus(7, { status: 'InProgress' }).subscribe((value) => (status = value.status));

    const request = http.expectOne('/api/v1/tasks/7/status');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({ status: 'InProgress' });
    request.flush({ status: 'InProgress' });
    expect(status).toBe('InProgress');
  });

  it('reassigns a task', () => {
    let newAssignee: number | undefined;

    service.reassign(7, { newAssigneeUserId: 9, reason: 'Role change' }).subscribe((value) => {
      newAssignee = value.assignedToUserId;
    });

    const request = http.expectOne('/api/v1/tasks/7/reassign');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ newAssigneeUserId: 9, reason: 'Role change' });
    request.flush({ assignedToUserId: 9 });
    expect(newAssignee).toBe(9);
  });

  it('loads recipients', () => {
    const recipients: TaskRecipient[] = [{ userId: 3, fullName: 'Joe' }];
    let result: TaskRecipient[] | undefined;

    service.getRecipients(7).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks/7/recipients');
    expect(request.request.method).toBe('GET');
    request.flush(recipients);
    expect(result).toEqual(recipients);
  });

  it('replaces recipients with a 204', () => {
    let completed = false;

    service.setRecipients(7, { recipientUserIds: [3, 9] }).subscribe(() => (completed = true));

    const request = http.expectOne('/api/v1/tasks/7/recipients');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ recipientUserIds: [3, 9] });
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(completed).toBeTrue();
  });

  it('loads comments', () => {
    const comments: TaskComment[] = [
      { id: 1, body: 'First', authorUserId: 3, authorName: 'Joe', createdAt: '2026-10-02T08:00:00Z' }
    ];
    let result: TaskComment[] | undefined;

    service.getComments(7).subscribe((value) => (result = value));

    const request = http.expectOne('/api/v1/tasks/7/comments');
    expect(request.request.method).toBe('GET');
    request.flush(comments);
    expect(result).toEqual(comments);
  });

  it('adds a comment', () => {
    let sent: unknown;

    service.addComment(7, { body: 'Agreed' }).subscribe((value) => (sent = value));

    const request = http.expectOne('/api/v1/tasks/7/comments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ body: 'Agreed' });
    request.flush({ id: 2, body: 'Agreed', authorUserId: 3, authorName: 'Joe', createdAt: '2026-10-03T08:00:00Z' });
    expect((sent as TaskComment).body).toBe('Agreed');
  });

  it('loads activity with query params', () => {
    const entries: ActivityEntry[] = [
      {
        id: 1,
        eventType: 'StatusChanged',
        actorUserId: 3,
        actorName: 'Joe',
        oldValue: 'ToDo',
        newValue: 'InProgress',
        message: 'Status changed',
        createdAt: '2026-10-03T08:00:00Z'
      }
    ];
    let result: ActivityEntry[] | undefined;

    service
      .getActivity(7, { eventType: 'StatusChanged', search: 'changed', from: '2026-10-01T00:00:00Z', to: '2026-10-31T00:00:00Z', actorUserId: 3 })
      .subscribe((value) => (result = value));

    const request = http.expectOne(
      '/api/v1/tasks/7/activity?eventType=StatusChanged&actorUserId=3&from=2026-10-01T00:00:00Z&to=2026-10-31T00:00:00Z&search=changed'
    );
    expect(request.request.method).toBe('GET');
    request.flush(entries);
    expect(result).toEqual(entries);
  });

  it('surfaces a 404 from getById as an error', () => {
    let error: unknown;

    service.getById(999).subscribe({ error: (err) => (error = err) });

    http.expectOne('/api/v1/tasks/999').flush({}, { status: 404, statusText: 'Not Found' });
    expect(error).toBeDefined();
  });

  it('surfaces a 403 from changeStatus as an error', () => {
    let error: unknown;

    service.changeStatus(7, { status: 'Completed' }).subscribe({ error: (err) => (error = err) });

    http.expectOne('/api/v1/tasks/7/status').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(error).toBeDefined();
  });
});