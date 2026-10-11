# TaskFlow — Project Overview

**TaskFlow – Intelligent Task Allocation & Tracking System**
MCA Academic Mini Project · Designed 2026-10-11

This document is the entry point for the codebase: what TaskFlow is, how it is built,
where everything lives, and the conventions that keep it consistent. The authoritative
design lives in `../Artifacts/TDD-TaskFlow.md`; this file is a condensed, actionable
mirror of it.

---

## 1. What TaskFlow Is

TaskFlow lets an organization allocate work to employees, track progress, and never miss a
deadline. Managers create and assign tasks; employees work them and comment; a background
scheduler automatically reminds the assignee and selected recipients as deadlines approach
and pass. Administrators manage people, departments, and system settings, and everyone gets
role-scoped dashboards, workload views, activity history, and exportable reports.

**Three roles:** Administrator, Manager, Employee.

---

## 2. Goals & Scope

1. Simplify employee task allocation.
2. Monitor progress of assigned tasks.
3. Reduce missed deadlines via automatic notifications.
4. Notify the assigned employee and selected recipients about approaching deadlines.
5. Help managers identify pending and overdue tasks.
6. Improve manager–employee communication (comments).
7. Maintain a history of task updates and changes.
8. Generate reports for planning; support CSV export.

**In scope:** the 20 screens, the deadline-notification workflow, business rules, and the
non-functional requirements defined in the BRD.
**Out of scope (future):** real-time push (SignalR), file attachments, recurring tasks,
calendar integration, email-token password reset.

---

## 3. Technology Stack

| Layer | Technology | Purpose |
|---|---|---|
| Backend runtime | .NET 8+ LTS (recommend .NET 10 LTS) | API host |
| Web framework | ASP.NET Core Web API | REST/JSON endpoints |
| ORM | Entity Framework Core (Code-First) | Data access, migrations |
| Database | SQLite | Single-file persistent store |
| Scheduler | `BackgroundService` | Deadline reminders (~1 min tick) |
| Validation | FluentValidation | Server-side input validation |
| Auth | JWT bearer + `PasswordHasher<T>` (PBKDF2) | Tokens, RBAC |
| Logging | Serilog | Structured logs |
| Frontend | Angular 19 (VS SPA template) + Bootstrap 5 | SPA |
| SPA dev integration | `Microsoft.AspNetCore.SpaProxy` (`.esproj`) | Runs the Angular dev server on F5 |
| Charts | Chart.js (`ng2-charts`) | Dashboards / reports |
| Drag & drop | Angular CDK | Kanban board |
| Backend tests | xUnit + `WebApplicationFactory` | Unit / integration |
| Frontend tests | Jasmine/Karma + Playwright | Unit / e2e |

Full rationale and the BRD→stack mapping are in TDD §4.

---

## 4. Architecture at a Glance

```
┌──────────────────────────┐   HTTPS / JSON   ┌──────────────────────────────────┐
│  Angular SPA (browser)   │ ◄──────────────► │  ASP.NET Core Web API (.NET)     │
│  standalone components   │   Bearer JWT     │  Controllers (thin)              │
│  route guards, services  │                  │  Application services/validators │
│  Bootstrap, Chart.js     │                  │  Domain entities/rules           │
└──────────────────────────┘                  │  Infrastructure (EF Core, SMTP)  │
                                              │  ┌────────────────────────────┐  │
                                              │  │ ReminderSchedulerService   │  │
                                              │  │ (BackgroundService ~1 min) │  │
                                              │  └────────────────────────────┘  │
                                              └───────────────┬──────────────────┘
                                                              │ EF Core
                                                              ▼
                                                  ┌───────────────────────┐
                                                  │ SQLite (taskflow.db)  │
                                                  └───────────────────────┘
```

**Request lifecycle:**
`HTTP → CORS/HTTPS → JWT auth → role/resource authorization → Controller → Service
(validation + business rules) → EF Core → SQLite → DTO → JSON`.

**Key decisions**

| # | Decision | Rationale |
|---|---|---|
| D1 | Three-tier SPA + REST API | Clear separation, maintainability |
| D2 | EF Core Code-First migrations on SQLite | Reproducible schema |
| D3 | JWT bearer auth + Angular route guards | Stateless, standard for SPA |
| D4 | `BackgroundService` + DB dedup index | No external broker; restart-safe |
| D5 | Compute `overdue` at query time | Never goes stale |
| D6 | Soft-delete (`IsDeleted`) for history-bearing entities | Protects audit records |

---

## 5. Repository / Module Structure

```
TaskFlow Proj/
├─ backend/
│  ├─ TaskFlow.sln
│  ├─ src/
│  │  ├─ TaskFlow.Api/            # ASP.NET Core Web API (template *.Server, relocated)
│  │  ├─ TaskFlow.Application/    # services, DTOs, validators, interfaces
│  │  ├─ TaskFlow.Domain/         # entities, enums, pure business rules
│  │  └─ TaskFlow.Infrastructure/ # EF Core DbContext, migrations, email, scheduler
│  └─ tests/
│     ├─ TaskFlow.UnitTests/
│     └─ TaskFlow.IntegrationTests/
├─ frontend/                      # Angular 19 workspace + JavaScript project
│  ├─ taskflow.client.esproj      # VS JavaScript/TypeScript project (F5 + npm UI)
│  ├─ proxy.conf.js               # dev proxy: /api/*  →  backend
│  └─ src/app/
│     ├─ core/                    # auth service, guards, interceptors, models
│     ├─ shared/                  # reusable UI (tables, pagination, dialogs)
│     └─ features/                # auth, dashboard, employees, departments, tasks,
│                                 # notifications, settings, workload, reports,
│                                 # activity, profile
├─ database/
│  ├─ taskflow.db                 # created at runtime
│  └─ seed/                       # demo data scripts
├─ docs/                          # project documentation
├─ project.md                     # this file
└─ agent.md                       # instructions for AI/automation contributors
```

> Generated by the Visual Studio **"Angular and ASP.NET Core"** template (Angular 19), then
> relocated. The solution uses the `.esproj` JavaScript project system and
> `Microsoft.AspNetCore.SpaProxy`; startup is set to **multiple projects** with the API first.

**Backend layer dependency direction:** `Api → Application → Domain`;
`Infrastructure → Application/Domain`. No layer skips a layer.

| Project | Contains | Depends on |
|---|---|---|
| `TaskFlow.Domain` | Entities, enums, business rules | — |
| `TaskFlow.Application` | Services, DTOs, validators, mapping | Domain |
| `TaskFlow.Infrastructure` | `AppDbContext`, migrations, SMTP, scheduler | Application, Domain |
| `TaskFlow.Api` | Controllers, auth, middleware, DI | Application, Infrastructure |

> The authoritative reference for the backend/frontend design is TDD §5–§6 and §12.

---

## 6. Domain Model Summary

Entities: `users`, `departments`, `tasks`, `task_comments`, `task_recipients`,
`notifications`, `reminder_history`, `email_delivery_logs`, `task_activity_logs`,
`application_settings`, `refresh_tokens`.

Enumerations:

| Enum | Values |
|---|---|
| `Role` | Administrator, Manager, Employee |
| `TaskPriority` | Low, Medium, High, Critical |
| `TaskStatus` | ToDo, InProgress, Blocked, Completed, Cancelled |
| `NotificationType` | TaskAssigned, Reminder, DueToday, Overdue, Reassigned, Comment |
| `ReminderType` | TwoDaysBefore, OneDayBefore, DueToday, Overdue |
| `ReminderStatus` | Pending, Sent, Failed, Skipped |
| `EmailStatus` | Sent, Failed |
| `ActivityAction` | Created, Assigned, Reassigned, StatusChanged, DeadlineChanged, PriorityChanged, Completed, CommentAdded, Updated, Deleted |

Rules that shape the data:
- All timestamps stored in **UTC**; a single configured timezone (`Application.Timezone`)
  is applied for "due today" and display.
- **Overdue** is derived: `Deadline < nowUtc AND Status NOT IN (Completed, Cancelled)`.
- Reminder dedup key: `(TaskId, RecipientUserId, ReminderType, ScheduledFor)` — unique.
- History-bearing entities are soft-deleted (`IsDeleted`); deactivation (`IsActive=false`)
  is separate and blocks login.

Full tables, indexes, and the ER diagram are in TDD §7.

---

## 7. Prerequisites & Build/Run

### Prerequisites

- **Visual Studio 2022 17.8+** (or VS 2026) with the **ASP.NET and web development** workload.
- **.NET 10 SDK** (`dotnet --list-sdks` shows a 10.0.x SDK).
- **Node.js** — **22 LTS recommended**. Angular 19 supports
  `^18.19.1 || ^20.11.1 || ^22.0.0`; Node 24 builds but shows an unsupported-engine warning.
- **Angular CLI 19** (global) — required by the VS SPA template:
  `npm install -g @angular/cli@19`.

### Run both from Visual Studio (recommended)

1. Open `backend/TaskFlow.sln`.
2. Right-click the solution → **Properties** → set **Startup Project** to **Multiple projects**,
   with `TaskFlow.Api` **first** and the frontend `.esproj` second.
3. Press **F5**. Visual Studio starts the API and launches the Angular dev server (`npm start`);
   the browser opens the SPA, which calls the API through `proxy.conf.js`.
   - If the frontend starts before the API you may see a proxy `ECONNREFUSED` error — refresh once
     the API is up, and keep the API first in the startup order.

### Backend (CLI)

```
dotnet build
dotnet ef database update     # apply migrations, seed admin
dotnet run --project src/TaskFlow.Api
```

### Frontend (CLI)

```
npm install
npm start                     # dev server (ng serve)
npm run build                 # production
```

**Serving:** Angular `dist` is served as static files (by the API or a static host); the
API runs behind HTTPS.

**Seeding:** an initial Administrator account plus demo departments/employees/tasks for the
"sample data for demonstration" deliverable.

Configuration sources: `appsettings.json` (non-secret defaults), environment
variables/user-secrets (JWT key, SMTP password), and the `application_settings` table
(runtime-editable, non-secret). See TDD §16.

---

## 8. Conventions

- **Layering:** never skip a layer; controllers stay thin and delegate to services.
- **Validation:** FluentValidation on every write DTO; server-side is authoritative,
  client-side is UX only.
- **Errors:** global exception middleware returning RFC-7807 `ProblemDetails`.
- **API:** base path `/api/v1`; `camelCase` JSON; list endpoints return
  `{ items, total, page, pageSize }`.
- **Time:** store UTC; only convert at the edge using the configured application timezone.
- **Deletion:** soft-delete entities with history; never hard-delete referenced rows.
- **Secrets:** never stored in the DB, returned by an API, or written to logs.
- **Security:** every endpoint has an explicit role/resource policy; never trust
  client-supplied IDs for ownership.
- **Frontend:** Angular signals for local state, RxJS for HTTP; feature folders are
  lazy-loaded; server validation is never replaced by client validation.

---

## 9. Related Documents

| Document | Location |
|---|---|
| Technical Design Document | `../Artifacts/TDD-TaskFlow.md` |
| Business Requirements (BRD v1.0) | `../Requirements/BRD for Task management.docx` |
| Screen specs 01–20 | `../Requirements/screens wise feature/` |
| OpenSpec workspace | `../openspec/` |
