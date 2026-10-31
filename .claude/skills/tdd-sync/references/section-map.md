# TDD section map: what each section must contain and where the facts come from

Keep this order and these numbers (TDD v2.1 numbering). The HLD and LLD sets cite TDD sections by number, so never
renumber an existing section: add new material as a new sub-section or at the end. Sections marked **NEW** did not
exist in TDD v1.0.

| # | Section | Must contain | Fact source |
|---|---|---|---|
| – | Header + Revision History | Document, Version, Status, Basis, Stack (with real versions), Project type, Date, Author, copyright; revision table | fact sheet §1–2, previous TDD header |
| 1 | Introduction | Purpose, audience, scope (20 screens + what was added beyond the BRD), definitions (add MFA, TOTP, TFA, permission key, custom role) | README, module overview docs |
| 1.5 NEW | Reference Documents | Every project document with purpose, location and version/status: approved BRD set, FRD, HLD, LLD, this TDD, original BRD docx, Development Plan, permission documents, App/Tech workflow docs, README, project.md, agent.md, OpenSpec, documentation skills. Check each path exists before listing it | `TF - Documents/` listing, Document Control of each `*-TaskFlow-Overall.md` |
| 1.6 NEW | Document Hierarchy & Traceability | Document-chain diagram; ID schemes (BRD/FRD/HLD/LLD); sub-module map: code, folder, stem, original BRD screens, TDD sections, workflow doc folders (17 rows) | BRD/FRD/HLD/LLD overall docs, workflow doc folders |
| 2 | Design Goals & Constraints | BRD goals; constraints (SQLite single file, data folder `C:\TaskFlow`, single instance, offline-capable, app timezone); key decisions table updated to the real ones (single API project layer-first, permission-based RBAC, mandatory MFA, internal mail instead of SMTP, AI providers) | README, project.md, Config/, Persistence/StorageRoot.cs |
| 3 | System Architecture | Updated diagram (Angular SPA ↔ API layers ↔ SQLite; ReminderSchedulerService; AI providers Ollama/Gemini/OpenAI; local file store for attachments; key ring). Runtime components; request lifecycle incl. MFA gate, session validation, ValidationFilter, GlobalExceptionHandler | fact sheet §9, Middleware/, Filters/ |
| 4 | Technology Stack | Real packages and versions (backend + frontend); remove what is not installed (e.g. Serilog, Playwright if absent); BRD→stack mapping kept | fact sheet §1–2 |
| 5 | Solution Structure | Real repo layout (single `TaskFlow.Api` project, layer-first with module sub-folders; tests mirror code); frontend `modules/<module>/<sub-module>/{pages,components,services,models,utils}`; enforcement (ArchitectureTests, `npm run check:structure`) | README "Backend/Frontend structure", fact sheet §9–11 |
| 6 | Backend Design | Cross-cutting (validation filter, ProblemDetails, exception types → status codes, logging, CORS, versioning); service catalogue by module (real class names); authorization model (permission policies, TaskAccess handler, scopes Own/Team/Organization) | fact sheet §9, Common/Exceptions, Authorization/ |
| 7 | Data Model | Mermaid ER of real entities; one table per entity; enums; unique indexes & FKs; soft delete & query filters; time/timezone & overdue derivation; migrations list | fact sheet §5–7, AppDbContext |
| 8 | API Design | Conventions; endpoint tables per module with verb, route, permission/policy, purpose; one example request per main write endpoint | fact sheet §3, Dtos/ |
| 9 | Authentication & Authorization | Login (email/username), MFA verify step, refresh-token cookie, logout, session/security stamp, password recovery via authenticator, attempt limits; roles (system + custom), permission catalog by module with role defaults; legacy keys | Services/Auth, Integrations/Security, PermissionCatalog.cs |
| 10 | Business Logic | Task create/edit/status transitions (real enum incl. InQA)/reassign/recipients/comments edit+delete/hours; departments, projects, positions, allocations rules; profile rules | Services/*, Common/Rules, module tech docs |
| 11 | Notification & Reminder Engine | Scheduler cadence, ReminderType (FirstReminder/SecondReminder/DueToday/Overdue), per-task intervals, Daily/Weekly overdue, dedup index, recalculation, channels (in-app + internal mail copy), NotificationType values | Services/Communication/Notification, ReminderCalculator, ReminderSchedulerService |
| 12 | Frontend Design | Shell, guards, interceptors, permission directive, nav built from permissions; screen → route → component → guard table from the real routes | fact sheet §10, layouts/nav-items.ts, shared/auth |
| 13 | Screen-to-Implementation Traceability | 20 BRD screens → real route, component, service, endpoints; plus rows for added screens (Mail, Projects, Role Configuration, TFA Settings, Security Setup, Account Recovery, Legal) | §8, §12 |
| 14 | Security Design | BRD NFR controls updated: PBKDF2, MFA/TOTP + backup codes, permission policies, resource checks, attempt limiting, DPAPI key ring, secrets handling, CORS, headers | Integrations/, Config/, README |
| 15 NEW | Internal Mail | Folders, compose/drafts/reply/forward, attachments limits and storage, privacy | Services/Communication/Mail, README |
| 16 NEW | TFA Assistant | Architecture (chat → model gateway → tools → confirm-before-act), providers, permissions, audit, rate limit, help knowledge, voice | Services/Assistant, Integrations/Ai, README |
| 17 | Non-Functional Design | Performance, reliability, usability, maintainability (architecture tests), compatibility, backup | as before + tests |
| 18 | Configuration & Deployment | Config sources; real appsettings keys and defaults (secrets as `<secret>`); data folder; run commands (SpaProxy, ports 7055/4200); seeding rules (`Seed:Enabled`) | fact sheet §8, README |
| 19 | Backup & Recovery | `C:\TaskFlow` contents (db, mail-attachments, dp-keys), what to back up together/apart | README |
| 20 | Testing Strategy | Real test projects, counts, categories (architecture, unit, integration), frontend structure check + Karma; no tool that isn't installed | fact sheet §11 |
| 21 | Acceptance Criteria Mapping | Original BRD §8 criteria → approved BRD ID (`AC-01 – AC-26` from §14 of `BRD-TaskFlow-Overall.md`) → TDD sections; add rows for approved criteria the original BRD did not have | approved overall BRD, previous TDD |
| 22 | Risks, Assumptions & Open Questions | Update: close answered questions (manager scope, token lifetime, CSV columns) with the real answer; add known gaps from module docs section 12; §22.5 lists which sub-module documents record each gap as "To be confirmed" | module tech docs, BRD/FRD/HLD/LLD sub-module docs |
| 23 NEW | Deviations from BRD | Table: BRD item, implemented as, reason/notes (e.g. Screen 14 SMTP superseded by internal mail, Kanban has In QA column, reports use Project filter) | module docs, screens specs |
| 24 | Future Enhancements | Remaining real future items only | previous + gaps |
| App. A | Task status transitions | Real transitions from the rules class (incl. InQA) as Mermaid stateDiagram | Common/Rules, Services/Monitoring/.../Rules |
| App. B | Reminder slot calculation | Example with real ReminderType names and settings | ReminderCalculator |
| App. C NEW | Permission catalog | Full key list by module with role defaults | PermissionCatalog.cs |
