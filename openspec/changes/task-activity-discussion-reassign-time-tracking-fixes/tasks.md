# Tasks

## 1. Discussion Comments on Edit Task Screen
- [x] 1.1 Review task-details component to add comments display area (frontend/src/app/features/tasks/task-details.component.*). Verify "No comments yet" shows when empty.
- [x] 1.2 Fetch discussion comments for task (reuse TaskDiscussionService or API) and render in task-details. Verify comments appear when they exist.

## 2. Activity History Filters
- [x] 2.1 Update ActivityComponent to read URL query params (ActivatedRoute.queryParamMap) and initialize filters (frontend/src/app/features/tasks/activity.component.ts). Verify filters apply from navigation.
- [x] 2.2 Ensure ActivityService passes filters correctly to backend API. Verify filtering by event type/date/user/search works.

## 3. Reassign History Visibility
- [x] 3.1 Verify Reassigned events are logged by ReassignAsync (backend/src/TaskFlow.Infrastructure/Services/TaskService.cs). Verify events appear in Activity History.
- [x] 3.2 Check activity filtering/permissions don't hide Reassigned events. Verify reassignment shows up with old/new assignee details.

## 4. Time Tracking in Links Tab + Activity History
- [x] 4.1 Add time tracking UI in Links tab (frontend/src/app/features/tasks/task-details.component.html/ts). Allow adding worked hours (e.g., today). Verify UI present.
- [x] 4.2 Implement hours logging API call to log worked hours (use existing hours logging path). Verify hours are logged.
- [x] 4.3 Compute and display remaining hours = estimated - actual (handle nulls) in task-details. Verify calculation.
- [x] 4.4 Ensure time tracking events appear in Activity History with details. Verify logged hours show in activity.
