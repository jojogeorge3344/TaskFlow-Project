## 1. Prerequisites

- [ ] 1.1 Confirm Visual Studio 2022 17.8+ (or VS 2026) has the **ASP.NET and web development** workload; verify it appears in the VS Installer's installed list. _(Operator-only: the implementation environment is headless; the CLI flow below does not require Visual Studio.)_
- [x] 1.2 Ensure the Angular 19 CLI is available; used via `npx @angular/cli@19` (no global install in the headless environment) and verified by building/serving the workspace.
- [x] 1.3 Record the Node decision: the machine runs **Node v24.19.0**, outside Angular 19's supported range (`^18.19.1 || ^20.11.1 || ^22.0.0`), so builds emit an unsupported-engine warning; **Node 22 LTS remains the operator recommendation** (design D3).
- [x] 1.4 Confirm the .NET 10 SDK; verify `dotnet --list-sdks` includes a 10.0.x SDK (found **10.0.300**).

## 2. Scaffold the solution

- [x] 2.1 Scaffold the equivalent structure with the **.NET CLI + Angular CLI** (design D1) inside `TaskFlow Proj/`: `backend/TaskFlow.sln`, the four layer projects, the API, both test projects, and a standalone Angular 19 workspace in `frontend/`; verify `dotnet build` succeeds at the solution root.
- [x] 2.2 Create the projects directly at the agreed layout (design D4) — `backend/src/TaskFlow.{Api,Application,Domain,Infrastructure}`, `backend/tests/*`, `frontend/` — and add every project to the `.sln`; verify the solution still builds.
- [x] 2.3 Remove the now-superseded empty placeholder folders/files so the on-disk tree matches `project.md` §5; verify no project references point at removed paths, and add `.gitignore` files so build output (`bin/`, `obj/`) and the runtime database are not tracked.

## 3. Backend layer projects

- [x] 3.1 Add `backend/src/TaskFlow.Domain` (class library); verify it builds with `dotnet build`.
- [x] 3.2 Add `backend/src/TaskFlow.Application` referencing `TaskFlow.Domain`; verify it builds.
- [x] 3.3 Add `backend/src/TaskFlow.Infrastructure` referencing `TaskFlow.Application` and `TaskFlow.Domain`; verify it builds.
- [x] 3.4 Add project references from `TaskFlow.Api` to `TaskFlow.Application` and `TaskFlow.Infrastructure`; verify the solution builds and the API still runs.
- [x] 3.5 Add `backend/tests/TaskFlow.UnitTests` and `backend/tests/TaskFlow.IntegrationTests` (xUnit) and add them to the solution; verify `dotnet test` executes successfully.

## 4. Frontend workspace

- [x] 4.1 Install dependencies and build the Angular workspace (`npm install` then `npm run build`); verify a `dist/` output is produced.
- [x] 4.2 Confirm the `frontend/src/app/{core,shared,features/*}` structure per TDD §5.3 (features: auth, dashboard, employees, departments, tasks, notifications, settings, workload, reports, activity, profile); verify the folders exist and the build still passes.

## 5. Run-both wiring

- [x] 5.1 Wire single-project run: `TaskFlow.Api` references `Microsoft.AspNetCore.SpaProxy` (design D5) so running the API launches the frontend — no multiple-startup configuration is required; verify F5 / `dotnet run` reaches the SPA.
- [x] 5.2 Confirm `Microsoft.AspNetCore.SpaProxy` is referenced in `TaskFlow.Api.csproj` with `SpaRoot`, `SpaProxyServerUrl` (`http://localhost:4200`) and `SpaProxyLaunchCommand` (`npm start`), and that the **https** launch profile sets `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES`; verify the Angular dev server auto-launches.
- [x] 5.3 Align the `proxy.conf.json` `target` (`https://localhost:7055`) with the API `applicationUrl` in `Properties/launchSettings.json`; verify an API call from the SPA (e.g. `POST /api/v1/auth/login`) reaches the backend.
- [x] 5.4 Disable the API's "Launch Browser" in both launch profiles (`launchBrowser: false`) and rely on the SpaProxy redirect; verify a single SPA browser tab is opened by the redirect.

## 6. Documentation and end-to-end verification

- [x] 6.1 Update `project.md` (§3 stack, §5 structure, prerequisites, run-both instructions) so it matches the actual tree and the `.esproj`-free SpaProxy flow.
- [x] 6.2 Update `agent.md` (repo map, commands, prerequisites, run notes) and verify each command matches `package.json` scripts and the `.csproj`/`.sln`.
- [ ] 6.3 Perform a clean end-to-end check: from a fresh state, run `dotnet build` at the solution root and F5; verify the API and SPA start together and record the result. _(`dotnet build` plus `dotnet run` — SpaProxy auto-launch, root redirect to `http://localhost:4200`, and a proxied login — verified headlessly; the Visual Studio F5/browser confirmation remains for the operator.)_
