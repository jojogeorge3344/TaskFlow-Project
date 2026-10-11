## 1. Prerequisites

- [ ] 1.1 Confirm Visual Studio 2022 17.8+ (or VS 2026) has the **ASP.NET and web development** workload; verify it appears in the VS Installer's installed list.
- [ ] 1.2 Install Angular CLI 19 globally (`npm install -g @angular/cli@19`); verify `ng version` reports CLI 19.x.
- [ ] 1.3 Install Node 22 LTS (recommended, via nvm-windows) and select it; verify `node --version` reports 22.x. If not installed, record the decision to run on Node 24 with the unsupported-engine warning.
- [ ] 1.4 Confirm the .NET 10 SDK; verify `dotnet --list-sdks` includes a 10.0.x SDK.

## 2. Scaffold the Visual Studio solution

- [ ] 2.1 Create a new solution named `TaskFlow` from the **Angular and ASP.NET Core** template inside `TaskFlow Proj/`, with "Configure for HTTPS" enabled; verify the generated solution builds and F5 shows the sample SPA populated by the API.
- [ ] 2.2 Relocate the generated backend project to `backend/src/TaskFlow.Api` and the `.esproj` client workspace to `frontend/`, updating the `.sln`; verify the solution still builds and F5 still runs both projects.
- [ ] 2.3 Remove the now-superseded empty placeholder folders/files so the on-disk tree matches `project.md` §5; verify no project references point at removed paths.

## 3. Backend layer projects

- [ ] 3.1 Add `backend/src/TaskFlow.Domain` (class library); verify it builds with `dotnet build`.
- [ ] 3.2 Add `backend/src/TaskFlow.Application` referencing `TaskFlow.Domain`; verify it builds.
- [ ] 3.3 Add `backend/src/TaskFlow.Infrastructure` referencing `TaskFlow.Application` and `TaskFlow.Domain`; verify it builds.
- [ ] 3.4 Add project references from `TaskFlow.Api` to `TaskFlow.Application` and `TaskFlow.Infrastructure`; verify the solution builds and the API still runs.
- [ ] 3.5 Add `backend/tests/TaskFlow.UnitTests` and `backend/tests/TaskFlow.IntegrationTests` (xUnit) and reference them to the solution; verify `dotnet test` executes successfully (zero tests is acceptable at this stage).

## 4. Frontend workspace

- [ ] 4.1 Install dependencies and build the Angular workspace (`npm install` then `npm run build`); verify a `dist/` output is produced.
- [ ] 4.2 Create the `frontend/src/app/{core,shared,features/*}` structure per TDD §5.3 (features: auth, dashboard, employees, departments, tasks, notifications, settings, workload, reports, activity, profile); verify the folders exist and the build still passes.

## 5. Run-both wiring

- [ ] 5.1 Set the solution startup to **Multiple projects** with `TaskFlow.Api` first and the frontend second; verify F5 launches both and the browser reaches the SPA.
- [ ] 5.2 Confirm `Microsoft.AspNetCore.SpaProxy` is referenced in `TaskFlow.Api.csproj` with `SpaProxyServerUrl` (e.g. `https://localhost:4200`) and `SpaProxyLaunchCommand` (`npm start`); verify F5 auto-launches the Angular dev server.
- [ ] 5.3 Align `proxy.conf.js` `target` with the API `applicationUrl` in `Properties/launchSettings.json`; verify an API call from the SPA (e.g. the sample endpoint) reaches the backend.
- [ ] 5.4 Apply the MS launch-profile guidance (disable the API's "Launch Browser" for its https profile) and verify a single SPA browser tab opens.

## 6. Documentation and end-to-end verification

- [ ] 6.1 Update `project.md` (§3 stack, §5 structure, prerequisites, run-both instructions) and verify it matches the actual tree and file names.
- [ ] 6.2 Update `agent.md` (repo map, commands, prerequisites, VS run notes) and verify each command matches `package.json` scripts and the `.csproj`/`.sln`.
- [ ] 6.3 Perform a clean end-to-end check: from a fresh clone-like state, run `dotnet build` at the solution root and F5; verify the API and SPA start together and record the result.
