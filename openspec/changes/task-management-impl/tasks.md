## 1. Domain & Enums

- [x] 1.1 Add TaskPriority and TaskStatus enums if missing (ensure match TDD)
- [x] 1.2 Add Task entity and related entities (TaskRecipient, TaskComment minimal)
- [x] 1.3 Update DbContext with Task sets and relationships
- [x] 1.4 Migration for tasks tables

## 2. Application Layer

- [ ] 2.1 DTOs for Task (TaskDto, CreateTaskRequest, UpdateTaskRequest, TaskListFilter)
- [ ] 2.2 Validators
- [ ] 2.3 ITaskService and implementation
- [ ] 2.4 Register in DI

## 3. API

- [ ] 3.1 TasksController with list/create/get/update/status endpoints

## 4. Frontend

- [ ] 4.1 Models and TaskService
- [ ] 4.2 TaskListComponent (/tasks)
- [ ] 4.3 TaskCreateComponent (/tasks/new)
- [ ] 4.4 Routes

## 5. Verification

- [ ] 5.1 Build and tests pass
- [ ] 5.2 Manual smoke for create/list
