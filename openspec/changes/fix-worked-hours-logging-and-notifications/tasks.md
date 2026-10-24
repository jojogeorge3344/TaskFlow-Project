## 1. Frontend: fix Log hours

- [x] 1.1 In `task-details.component.html`, replace `(ngSubmit)="logHours()"` on the log form with `(submit)="$event.preventDefault(); logHours()"`.
- [x] 1.2 In `logHours()` success, patch `editForm.actualHours` with the updated total when `editing()` is true.
- [x] 1.3 In `saveEdit()`, send `actualHours ?? 0` so a cleared field resets the total.

## 2. Backend: notify assigned members

- [x] 2.1 Inject `IMailService` into `TaskService` and confirm DI resolves it.
- [x] 2.2 Add `NotifyHoursChangedAsync` in `TaskService`: build the target set (assignee + recipients, distinct, including the user who made the change), publish an in-app `System` notification per target, and send one internal mail to their emails.
- [x] 2.3 Call it from `UpdateAsync` after `SaveChangesAsync`, only when hours changed. Wrap it in try/catch with logging so failures never undo the update.
- [x] 2.4 Word the message for added (increase), updated and cleared (new total 0).

## 3. Tests

- [x] 3.1 Component spec: clicking Log hours does not reload or navigate, shows the new Worked and Remaining values, and patches the edit form field.
- [x] 3.2 Component spec: clearing the field and saving sends `actualHours: 0`.
- [x] 3.3 Backend tests: add, update and clear each notify the right members including the actor, an unchanged total notifies nobody, and a mail failure still saves the hours.

## 4. Verify

- [x] 4.1 Run the frontend and backend test suites and a production build.
- [ ] 4.2 Manually check the Edit Task screen: log hours with no blank flash, the field updates, and another assigned user sees the notification and mail.
