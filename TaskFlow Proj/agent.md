# Agent Guide — TaskFlow

Operating instructions for AI assistants and automation contributors working in this
repository. Read this before making changes. For product context, see `project.md`; for the
authoritative design, see `../Artifacts/TDD-TaskFlow.md`.

---

## 1. Role & Scope

You help build and maintain TaskFlow: an ASP.NET Core Web API + SQLite backend with an
Angular SPA frontend, implementing the BRD. Work from the design documents, not from
assumption. When something is ambiguous or would change scope, ask before acting.

---

## 2. Repository Map

```
TaskFlow Proj/
├─ backend/                        # .NET solution
│  ├─ TaskFlow.sln
│  ├─ src/TaskFlow.Api/            # one project, layer-first: Controllers, Services, Repositories,
│  │                                 #   Dtos, Models, Integrations, Middleware, Config, Common, Persistence
│  └─ tests/                       # unit + integration
├─ frontend/                       # Angular 19 workspace (Angular CLI, standalone)
│  ├─ proxy.conf.json              # dev proxy: /api/*  →  https://localhost:7055
│  └─ src/app/                     # core, shared, features (lazy-loaded)
├─ database/                       # runtime DB + seed scripts
├─ docs/                           # documentation
├─ project.md                      # project overview (read this first)
└─ agent.md                        # this file
```

Planning material lives **outside** this folder, at the workspace root:

| Path | Contents | Treat as |
|---|---|---|
| `../Requirements/BRD for Task management.docx` | Business requirements | Source of truth for *what* |
| `../Requirements/screens wise feature/01..20*.md` | Per-screen specs | Source of truth for UI behaviour |
| `../Artifacts/TDD-TaskFlow.md` | Technical design | Source of truth for *how* |
| `../openspec/` | OpenSpec workspace (changes, specs) | Planning workflow |

Do **not** edit files under `.opencode/` (tooling/skills) as part of feature work.

---

## 3. Prerequisites & Commands

**Prerequisites:** .NET 10 SDK; **Node 22 LTS recommended** (Angular 19 supports
`^18.19.1 || ^20.11.1 || ^22.0.0`; the current machine runs Node 24 and warns); Angular CLI 19
via `npx @angular/cli@19` (no global install). **VS 2022 17.8+ / VS 2026** with the
**ASP.NET and web development** workload is *optional* (F5 + debugger only).

**Run both:** `dotnet run --project src/TaskFlow.Api` (default **https** profile), or open
`backend/TaskFlow.sln` and press **F5**. `Microsoft.AspNetCore.SpaProxy` launches the Angular
dev server (`npm start`) once the API is up and redirects the browser to `http://localhost:4200`;
the SPA proxies `/api/*` to `https://localhost:7055`. The API-only `http` profile does not launch
the SPA. (Run `npm install` in `frontend/` first.)

| Task | Command | Working dir |
|---|---|---|
| Build backend | `dotnet build` | `backend/` |
| Run API | `dotnet run --project src/TaskFlow.Api` | `backend/` |
| Apply migrations | `dotnet ef database update --project src/TaskFlow.Api` | `backend/` |
| Backend tests | `dotnet test` | `backend/` |
| Install frontend deps | `npm install` | `frontend/` |
| Run frontend | `npm start` | `frontend/` |
| Frontend unit tests | `ng test` | `frontend/` |
| Frontend e2e | `npx playwright test` | `frontend/` |

If a lint/typecheck/build command exists, run it after your change and fix what you broke.
If you cannot find the right command, ask.

---

## 4. Hard Rules

**Architecture**
- Respect the layer flow: Controller → Service → Repository → database, each through the interface of the layer
  below. Only `Persistence` and `Repositories` touch `AppDbContext`; repositories never save (services commit through
  `IUnitOfWork`). `ArchitectureTests` enforces this; see the "Backend structure" section of the workspace README.
- Controllers stay thin; business rules live in services (pure rules in `Services/<Module>/Rules` or `Common/Rules`).
- Put new code in the module folder of every layer it touches (`Controllers/<Module>/<SubModule>`, same path under
  `Services`, `Repositories`, `Dtos`), with the namespace matching the folder.

**Data**
- All timestamps stored in **UTC**. Convert only at the boundary using `Application.Timezone`.
- **Overdue is never stored** — derive it from `Deadline` + `Status` at query time.
- Soft-delete history-bearing entities (`users`, `departments`, `tasks`); never hard-delete
  referenced rows. `IsActive=false` (blocks login) is separate from `IsDeleted`.
- Reminder generation must be **idempotent** — rely on the unique dedup index
  `(TaskId, RecipientUserId, ReminderType, ScheduledFor)`; a duplicate insert is ignored.

**API**
- Base path `/api/v1`, `camelCase` JSON, `ProblemDetails` for errors.
- Every endpoint declares an explicit role/resource policy. Never trust client-supplied IDs
  for ownership — check assignee/creator/recipient/manager scope server-side.
- List endpoints return `{ items, total, page, pageSize }`.

**Security**
- Passwords only via `PasswordHasher<T>` (PBKDF2). Never log or return them.
- Secrets (JWT signing key, SMTP password) come from environment variables/user-secrets —
  never the database, never a response, never a log line.
- Generic authentication errors (no user enumeration); CORS restricted to known origins.

**Frontend**
- Server-side validation is authoritative; client validation mirrors it for UX only.
- Feature folders are lazy-loaded; use signals for local state, RxJS for HTTP.

---

## 5. Workflow

1. **Understand** the request against the BRD/TDD/screen spec. Ask if scope or observable
   behaviour is unclear.
2. **Plan with OpenSpec** for non-trivial changes: create a proposal, get specs/design/tasks,
   then implement. Start with the openspec workflow skills — do not hand-write directories
   under `../openspec/changes/`.
3. **Implement** in small, coherent steps that map to the task list.
4. **Verify** — build + tests for the affected layer. Never claim done without running them.
5. **Do not commit, push, or open a PR unless the user explicitly asks.** If asked, follow
   the repo's commit/PR workflow and never commit secrets.

---

## 6. Recording Decisions

- Changes to *what the system must do* → OpenSpec specs / the BRD.
- Changes to *how it is built* → TDD (`../Artifacts/TDD-TaskFlow.md`) and/or OpenSpec design.
- Changes to stack, structure, or conventions → update `project.md` in the same change.

Keep `project.md`, `agent.md`, the TDD, and OpenSpec specs in sync; do not duplicate facts
in more than one place without a clear canonical owner.

---

## 7. Guardrails

- Never add comments to code unless asked.
- Never introduce a library without checking the stack in `project.md` and the TDD.
- Never expand scope beyond the BRD; put ideas in the "Future Enhancements" backlog instead.
- Stop and ask when a decision materially affects scope, data, security, or acceptance criteria.
