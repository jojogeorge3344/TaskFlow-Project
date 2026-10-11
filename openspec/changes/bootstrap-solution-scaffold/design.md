## Context

See `proposal.md` — Why. Current state and constraints that shape the approach:

- `TaskFlow Proj/` currently contains only placeholders: `backend/src/TaskFlow.{Api,Application,Domain,Infrastructure}`,
  `backend/tests/*`, `frontend/src/app/*`, `database/seed/`, `docs/`,
  plus the docs `project.md` and `agent.md`. There is no `.sln`, no `.csproj`, no Angular workspace.
- Machine verified: .NET SDK **10.0.300**, Node **v24.19.0**, npm **11.17.0**; no global Angular CLI.
- The Visual Studio **"Angular and ASP.NET Core"** template requires a global **Angular CLI 19** and is
  not yet updated for Angular 20+ (Angular 20 triggers "Version of Angular CLI was not valid").
- The template generates multiple projects, `SpaProxy`, `proxy.conf.js`, and an NgModule-style app
  (`app.module.ts`), whereas TDD §3/§12 targets standalone components.

## Goals / Non-Goals

**Goals:**

- Pressing F5 in Visual Studio starts the API **and** the Angular dev server, with the browser
  showing the SPA served through the API proxy.
- Backend separated into the four TDD §5.2 layers, with unit/integration test projects present.
- A clean checkout can be built and run by following documented prerequisites and commands.
- `project.md` / `agent.md` describe the structure that actually exists on disk.

**Non-Goals:**

- Implementing any TaskFlow feature (entities, endpoints, auth, EF Core model, scheduler).
- Production publish/hosting, CI, or containerization.
- Migrating to Angular 20/21 in this change.

## Decisions

**D1 — Use the VS "Angular and ASP.NET Core" template (not hand-wiring).**
Rationale: it provides SpaProxy, multiple-startup, HTTPS, and proxy config out of the box, which is
exactly the requested F5 experience with the least custom configuration.
_Alternative considered:_ hand-add `.esproj` + SpaProxy to manually created projects — more moving
parts and more failure modes for the same result.

**D2 — Target Angular 19 (the template's native version).**
Rationale: eliminates the known template incompatibility with Angular 20.x and matches the approved
choice.
_Alternative considered:_ Angular 20/21 via the "create API first, add Angular project" workaround —
deferred; can be a follow-up `ng update` (19→20→21) once the template supports it.

**D3 — Recommend Node 22 LTS for the Angular 19 build.**
Rationale: Angular 19 officially supports Node `^18.19.1 || ^20.11.1 || ^22.0.0`; the installed
Node 24 is outside that range and produces an unsupported-engine warning. Installing Node 22 LTS via
nvm-windows removes the warning.
_Alternative:_ stay on Node 24 and accept the warning (usually builds). Deferred to the operator
(see Open Questions).

**D4 — Adopt the agreed layout, relocating template output.**
Map `TaskFlow.Server` → `backend/src/TaskFlow.Api` and the `.esproj` client workspace → `frontend/`.
Rationale: keeps the documented `backend/` + `frontend/` tree and TDD §5.1 intent.
_Alternative:_ keep template default names (`TaskFlow.Server` / `taskflow.client`) — rejected to
avoid diverging from the TDD and project.md.

**D5 — API starts before the frontend.**
Set multiple-startup projects with the API first. Rationale: prevents the `[HPM] ECONNREFUSED`
proxy error that occurs when the SPA dev server proxies to a backend that is not up yet.

**D6 — Keep the template's NgModule app for this change; convert to standalone later.**
Rationale: keeps the bootstrap minimal and reversible; the standalone migration
(`ng generate @angular/core:standalone`) is mechanical and better bundled with the first feature.
_Alternative:_ convert during scaffolding — deferred to reduce bootstrap risk.

## Risks / Trade-offs

- **Node 24 vs Angular 19 unsupported engines** → use Node 22 LTS (D3), or accept the warning.
- **Template pinned to Angular 19** → future upgrade friction; mitigate by planning the incremental
  `ng update` path (19→20→21) as a later change.
- **Relocating template output can break `.sln`/relative paths** → perform renames via the solution
  and verify the solution builds and F5 runs both projects after each move.
- **Frontend/backend start-order race** → enforce API-first startup (D5).
- **NgModule vs standalone divergence from TDD** → tracked in D6; convert in a follow-up change.
- **Angular CLI version drift on the machine** → pin the global CLI to 19 before scaffolding; the
  project's own `package.json` remains authoritative afterward.
