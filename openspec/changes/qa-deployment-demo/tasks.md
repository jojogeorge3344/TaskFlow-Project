# Tasks

## 1. Branch

- [x] 1.1 Create branch `QA-Deployment` from `Develop_env` and verify `git branch --show-current` prints `QA-Deployment`

## 2. API serves the SPA

- [x] 2.1 In `Program.cs`, add `UseDefaultFiles`/`UseStaticFiles` and a `MapFallbackToFile("index.html")` that excludes `/api`, active only when `wwwroot/index.html` exists, and verify the app still builds and Development behaves as before
- [x] 2.2 Add integration tests: root returns the client page, `/admin` returns the client page, unknown `/api/v1/nope` returns 404, and verify `dotnet test` passes
- [x] 2.3 Confirm the production Angular build calls relative `/api` only (no `localhost` URLs in `dist`) and verify with a search of the build output

## 3. QA configuration

- [x] 3.1 Add `appsettings.QA.json` with `Seed:Enabled=true`, and verify the QA app creates the three demo users on an empty data folder
- [x] 3.2 Verify startup fails with a clear message when `Jwt__Key` is absent, and add a test or documented manual check for it
- [x] 3.3 Verify `Storage__Root` points the database, attachments and key ring at the chosen folder, and that a restart keeps the data

## 4. Demo script

- [x] 4.1 Write `scripts/qa-run.ps1`: check for dotnet, node and `cloudflared` and stop with an install hint if one is missing; verify by running it with `cloudflared` renamed
- [x] 4.2 In the script, build the SPA, publish the API, copy the SPA build into the published `wwwroot`, and verify the published folder contains `index.html`
- [x] 4.3 In the script, generate and persist a random `Jwt__Key` in the data folder (never in the repo), set `ASPNETCORE_ENVIRONMENT=QA`, start the API on a local HTTP port, and verify the sign-in page loads at `http://localhost:<port>`
- [ ] 4.4 In the script, start the quick tunnel, read and print the `trycloudflare.com` URL, set `Mail__AppBaseUrl` from it, and stop both processes on exit; verify no processes remain after Ctrl+C
- [ ] 4.5 Verify through the public URL that admin, manager and employee can sign in, the refresh cookie works, and a task can be created

## 5. Documentation

- [x] 5.1 Add a "Demo hosting" section to `README.md` covering prerequisites, the one command, the changing URL, data location, seeded passwords and how to stop; verify a fresh reader can follow it as written
- [x] 5.2 Confirm `Login creds.txt`, the data folder and the generated key are not tracked by git, and add ignore entries if needed; verify with `git status`

## Workflow follow-up

- Open a pull request from `QA-Deployment` after review.
- Archive the change once the demo is verified.
