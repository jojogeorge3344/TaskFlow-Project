## Context

See `proposal.md` — Why. Current state and constraints that shape the approach:

- `TaskFlow Proj/` currently contains only placeholders: `backend/src/TaskFlow.{Api,Application,Domain,Infrastructure}`,
  `backend/tests/*`, `frontend/src/app/*`, `database/seed/`, `docs/`,
  plus the docs `project.md` and `agent.md`. There is no `.sln`, no `.csproj`, no Angular workspace.
- Machine verified: .NET SDK **10.0.300**, Node **v24.19.0**, npm **11.17.0**; no global Angular CLI.
- **Revised during implementation:** the execution environment is headless (no Visual Studio), so the
  **.NET CLI + Angular CLI** are used to produce the equivalent structure (see D1/D2 below). `SpaProxy`
  and `proxy.conf.json` are wired explicitly. No `.esproj` is used: it requires the VS-only
  `Microsoft.VisualStudio.JavaScript.Sdk`, which is unresolvable here and would break `dotnet build`
  at the solution root; SpaProxy already launches the dev server, so F5 needs only the API project.
- The originally planned VS template would have required a global **Angular CLI 19** and is not updated
  for Angular 20+ (Angular 20 triggers "Version of Angular CLI was not valid"); it also emits an
  NgModule-style app (`app.module.ts`) whereas TDD §3/§12 targets standalone components.

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

**D1 — Scaffold with the .NET CLI and Angular CLI (revised: no Visual Studio in the environment).**
Rationale: the headless implementation environment has .NET SDK 10 and Node/npm but no Visual Studio,
so the VS "Angular and ASP.NET Core" template cannot be run. The same structure — solution, four
backend layers, API, test projects, Angular workspace — is produced with `dotnet new` and the Angular
CLI, and `SpaProxy` / `proxy.conf.json` are wired explicitly.
_Alternative considered (original D1):_ the VS template — provides SpaProxy/HTTPS/F5 out of the box but
requires an interactive Visual Studio session, so it is not executable here; the F5 experience it
targets remains the goal and is documented for the operator.

**D2 — Target Angular 19 via the Angular CLI.**
Rationale: matches the approved stack and the TDD baseline; pin the CLI to 19 (`npx @angular/cli@19`).
Angular 20+ is avoided as before.
_Alternative considered:_ latest Angular — rejected to stay consistent with the approved choice; a
later incremental `ng update` (19→20→21) remains the upgrade path.

**D3 — Recommend Node 22 LTS for the Angular 19 build.**
Rationale: Angular 19 officially supports Node `^18.19.1 || ^20.11.1 || ^22.0.0`; the installed
Node 24 is outside that range and produces an unsupported-engine warning. Installing Node 22 LTS via
nvm-windows removes the warning.
_Alternative:_ stay on Node 24 and accept the warning (usually builds). Deferred to the operator
(see Open Questions).

**D4 — Create projects directly at the agreed layout (no relocation needed).**
CLI scaffolding emits projects straight into `backend/src/TaskFlow.{Api,Application,Domain,Infrastructure}`,
`backend/tests/*`, and `frontend/`.
Rationale: with the CLI there is no template default name to relocate, so the `backend/` + `frontend/`
tree and TDD §5.1 are satisfied as-created.
_Alternative (original D4):_ generate under template names (`TaskFlow.Server` / `taskflow.client`) then
rename — unnecessary without the template.

**D5 — API starts before the frontend (via SpaProxy, not multiple-startup).**
`TaskFlow.Api` references `Microsoft.AspNetCore.SpaProxy`, so running the API (F5 or
`dotnet run`, default **https** profile) launches the Angular dev server after the API is
listening. Rationale: removes the `[HPM] ECONNREFUSED` proxy race without a multiple-project
startup configuration and keeps F5 working from a single project.

**D6 — Standalone components from the start.**
The Angular CLI (v19) scaffolds a standalone app (`bootstrapApplication` + `provideRouter`), which
already matches TDD §3/§12, so no NgModule conversion step is needed.
_Alternative considered (original D6):_ keep the VS template's NgModule app and convert later —
moot because the CLI output is standalone.

## Risks / Trade-offs

- **Node 24 vs Angular 19 unsupported engines** → use Node 22 LTS (D3), or accept the warning.
- **Template pinned to Angular 19** → future upgrade friction; mitigate by planning the incremental
  `ng update` path (19→20→21) as a later change.
- **Relative paths across `backend/` and `frontend/`** → keep the SpaProxy `SpaRoot` and the
  `proxy.conf.json` target aligned with `launchSettings.json`; verify by running the app.
- **Frontend/backend start-order race** → SpaProxy launches the SPA after the API (D5).
- **NgModule vs standalone divergence from TDD** → resolved: the Angular CLI scaffolds standalone (D6).
- **`.esproj` would break CLI builds** → omitted; SpaProxy provides the launch (see Context).
- **Angular CLI version drift on the machine** → pin the global CLI to 19 before scaffolding; the
  project's own `package.json` remains authoritative afterward.
