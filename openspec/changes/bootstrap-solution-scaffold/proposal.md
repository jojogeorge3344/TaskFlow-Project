## Why

The repository has approved requirements and a technical design, but no runnable
solution — `TaskFlow Proj/backend` and `TaskFlow Proj/frontend` are empty. Before any
feature work can begin, we need a Visual Studio solution that builds and starts the
ASP.NET Core API and the Angular 19 SPA together from a single **F5**, matching the
chosen stack (ASP.NET Core + EF Core + SQLite + Angular).

## What Changes

- Create a Visual Studio **"Angular and ASP.NET Core"** solution named `TaskFlow` inside
  `TaskFlow Proj/` (HTTPS enabled). The template scaffolds **Angular 19** and wires
  `SpaProxy`, multiple startup projects, HTTPS, and request proxying out of the box.
- Relocate the generated projects to the agreed layout: `…Server` → `backend/src/TaskFlow.Api`,
  the `.esproj` client workspace → `frontend/`.
- Add backend layer projects per TDD §5.2: `TaskFlow.Domain`, `TaskFlow.Application`,
  `TaskFlow.Infrastructure`, plus `TaskFlow.UnitTests` and `TaskFlow.IntegrationTests`.
- Verify the "run both from F5" wiring: multiple startup (API first), `Microsoft.AspNetCore.SpaProxy`
  with `SpaProxyLaunchCommand=npm start`, and `proxy.conf.js` forwarding `/api/*` to the API.
- Update `project.md` and `agent.md` with the real structure, prerequisites, and run instructions.

This change delivers **scaffolding and developer-experience configuration only**. No product
behavior (entities, endpoints, auth, scheduler) is implemented.

## Capabilities

This is a tooling/scaffolding change with no spec-level behavior change, so the change sets
`skip_specs: true` (recorded in `.openspec.yaml`).

### New Capabilities

_None_ — no system behavior is introduced.

### Modified Capabilities

_None_ — no existing requirements change.

## Impact

- **New files:** `TaskFlow.sln`, backend project(s) (`.csproj`), frontend `.esproj` +
  Angular workspace (`package.json`, `angular.json`, `proxy.conf.js`, `aspnetcore-https.js`).
- **Affected layout:** `TaskFlow Proj/` (`backend/`, `frontend/`); docs `project.md`, `agent.md`.
- **New dependencies:** `Microsoft.AspNetCore.SpaProxy`; dev tooling Angular CLI 19, Node (see risks).
- **Environment prerequisites:** VS 2022 17.8+ or VS 2026 with the **ASP.NET and web development**
  workload; .NET 10 SDK; Node + npm; Angular CLI 19.
