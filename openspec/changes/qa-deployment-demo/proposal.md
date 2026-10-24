# Proposal

## Why

TaskFlow needs to be shown to evaluators from a public URL, but there is no budget and no cloud account (no Azure). Today the Angular SPA and the .NET API run as two dev servers behind a dev proxy, so there is no single, repeatable way to run it as one app on a PC and make it reachable from the internet at zero cost.

## What Changes

- Add a `QA-Deployment` branch (from `Develop_env`) that carries the demo-hosting changes.
- Make the API able to serve the built Angular app (static files plus a fallback to `index.html` for client-side routes) so SPA and API share one origin. The SPA already calls a relative `/api/v1/...`, so no frontend URL change is needed.
- Add a `QA` environment configuration (`appsettings.QA.json`) that: enables the demo seed users, takes the JWT signing key and data folder from environment variables, and sets the public base URL used in links.
- Add a PowerShell script `scripts/qa-run.ps1` that builds the SPA, publishes the API, starts it on a local port, and starts a free Cloudflare quick tunnel, printing the public URL.
- Add a short "Demo hosting" section to the README (prerequisites, one command, how to stop, data location, caveats).
- No paid service, no cloud account, no database server. SQLite, attachments and the Data Protection key ring stay on the host PC's disk (`Storage:Root`).

## Capabilities

### New Capabilities
- `qa-demo-hosting`: Running TaskFlow as a single self-contained web app on a local PC in a QA configuration and reaching it from a public HTTPS URL at no cost.

### Modified Capabilities
<!-- None: no existing spec-level behavior changes; the project has no main specs yet. -->

## Impact

- Code: `TaskFlow.Api` (`Program.cs` static-file and SPA fallback middleware, new `appsettings.QA.json`, publish step that includes the SPA build output), `scripts/qa-run.ps1`, README.
- Config: environment variables `Jwt__Key`, `Storage__Root`, `Cors__AllowedOrigins__0`, `Mail__AppBaseUrl`, `ASPNETCORE_ENVIRONMENT=QA`.
- Dependencies: dev-time only on the host PC: .NET SDK, Node, and the `cloudflared` binary. No new NuGet or npm packages.
- Security: demo accounts use the seeded default passwords, so the URL is for a short-lived demo only. The JWT key is never committed.
- Availability: the demo is only up while the PC and the script are running, and the quick-tunnel URL changes on each start.
