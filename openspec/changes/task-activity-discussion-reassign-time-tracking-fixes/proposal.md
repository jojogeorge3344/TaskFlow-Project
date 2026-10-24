# Proposal

## Why

TaskFlow has four related UX/visibility issues in task management: comments added via Discussion aren't visible in the expected place on the Edit Task screen; the Activity History screen's filters don't work; reassigned tasks don't appear to be tracked/visible in history; and the Links tab's time tracking isn't properly integrated with Activity History (can't track hours with remaining/actual visibility). These break traceability and make it hard for users to see who did what and how much time was logged.

## What Changes

- **Discussion/Comments visibility**: Ensure comments posted via Discussion are visible/listed on the Edit Task screen in the expected location (above/beside tab context) with a proper "No comments yet" state.
- **Activity History filters**: Fix Activity History screen so event type/date/user/search filters apply correctly (respect URL query params from navigation).
- **Reassign history visibility**: Ensure reassignment actions are logged and visible in Activity History (and remain consistent with existing Reassigned events).
- **Time tracking in Links tab + Activity History**: Add proper time tracking in Links tab (track worked hours per entry/link/context), compute remaining hours = estimated - actual (handling nulls), and ensure all time tracking details appear in Activity History screen.

## Capabilities

### New Capabilities
- `task-activity-history`: Correctly filter and display all task activity events including Reassigned and time tracking events.
- `task-discussion-comments`: Display discussion comments on the Edit Task screen with proper empty state.
- `task-time-tracking`: Track worked hours from Links tab, show remaining/actual hours, and log to activity history.

### Modified Capabilities
<!-- None - all are new cohesive capabilities for this fix set -->

## Impact

Affected code (from exploration):
- Frontend: `TaskFlow Proj/frontend/src/app/features/tasks/task-details.component.*`, `activity.component.*`, `task-discussion.component.*`, `reassign.component.*`, `task.service.ts`
- Backend: `TaskFlow Proj/backend/src/TaskFlow.Api/Controllers/TasksController.cs`, `TaskFlow.Proj/backend/src/TaskFlow.Infrastructure/Services/TaskService.cs`, `TaskFlow.Infrastructure/Services/TaskActivityService.cs`
- Domain: `TaskFlow.Domain/Enums/TaskActivityEventType.cs` (Reassigned/HoursLogged/TimeLogged)
