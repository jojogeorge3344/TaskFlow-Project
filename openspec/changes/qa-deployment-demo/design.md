# Design

## Context

See proposal.md for motivation. Observed in the repo:

- Backend: ASP.NET Core (.NET) with SQLite via EF Core. The database, attachments and the Data Protection key ring all live under `Storage:Root` (`StorageRoot.Resolve`), so one folder holds all state.
- Frontend: Angular, production build to `frontend/dist/taskflow-frontend`. Services call relative `/api/v1/...`; `proxy.conf.json` maps `/api` to `https://localhost:7055` in development only.
- `Program.cs` has no static-file or SPA fallback middleware. CORS allows only `http://localhost:4200`. `UseHttpsRedirection` is on. The refresh cookie is `Secure` and `SameSite=Strict`.
- `Jwt:Key` defaults to empty and is not set in `appsettings.json`, so it must come from configuration.
- Seed users (admin, manager, employee) are created only when `Seed:Enabled` is true, which is currently set only in `appsettings.Development.json`.
- `ForwardedHeaders` trusts only `X-Forwarded-For` from loopback proxies. A local `cloudflared` process connects from loopback, so no extra proxy configuration is required.

## Goals / Non-Goals

**Goals:**
- One process, one origin, reachable over public HTTPS, started by one script.
- Zero cost and zero accounts.
- No change to behavior in Development or in existing tests.

**Non-Goals:**
- Production-grade hosting, high availability, a stable domain, or a real CI/CD pipeline.
- Changing the database engine or moving state off the local disk.
- Hardening the seeded demo passwords beyond documenting that the URL is short-lived.

## Decisions

1. **API serves the SPA** (`UseDefaultFiles`/`UseStaticFiles` plus `MapFallbackToFile("index.html")`, with `/api` excluded from the fallback) rather than hosting the SPA separately. Same origin means no CORS changes, the `SameSite=Strict` cookie works, and only one tunnel is needed. Alternative: SPA on Cloudflare Pages plus API tunnel. Rejected: two origins, CORS and cookie problems, two things to run.
2. **SPA files are copied into the API's `wwwroot` at publish time** by the script, not committed. Keeps build output out of git and leaves Development unchanged, since the middleware only serves files if `wwwroot/index.html` exists. Alternative: MSBuild target in the csproj that runs `npm run build`. Rejected for the demo: slower, and ties every backend build to Node.
3. **New `QA` environment** (`appsettings.QA.json`) rather than flipping settings in `appsettings.json`. It sets `Seed:Enabled=true`, and the script sets `ASPNETCORE_ENVIRONMENT=QA`. Development and Production defaults stay as they are. Secrets (`Jwt__Key`, `Storage__Root`) come from environment variables. The script generates a random `Jwt__Key` per machine and stores it in the data folder so tokens survive a restart.
4. **Cloudflare quick tunnel (`cloudflared tunnel --url http://localhost:<port>`)** for public access. Free, no account, HTTPS provided by Cloudflare. Alternatives: ngrok (needs an account and has a free-tier interstitial), Render free (wipes the SQLite state on redeploy), Azure F1 (needs an account). The API listens on plain HTTP locally, since TLS ends at the tunnel.
5. **Public URL discovery**: the script reads the `https://*.trycloudflare.com` address from `cloudflared` output and prints it. `Mail:AppBaseUrl` and CORS are set from it at start-up through environment variables so links in mail point to the live URL.
6. **HTTPS redirect**: with only an HTTP endpoint and no HTTPS port configured, `UseHttpsRedirection` does not redirect, so it can stay. The tunnel's `X-Forwarded-Proto` is not trusted, which is acceptable for this demo.

## Risks / Trade-offs

- [Quick-tunnel URL changes on every start] → The script prints it each time; document that links shared earlier stop working. A named tunnel with a free Cloudflare account is a later option.
- [Default seeded passwords on a public URL] → Short-lived demo only; stop the script when done; the README states this and lets the demo override the passwords via `Seed__AdministratorPassword` and similar variables.
- [PC sleeps or goes offline, so the demo is down] → Document the power setting; the demo is attended.
- [SPA fallback shadowing API routes] → Exclude `/api` from the fallback and cover with a test on an unknown `/api` path.
- [Windows DPAPI key protection ties the key ring to the Windows account] → Fine on the same PC; documented that copying the data folder to another account will not decrypt stored AI keys.
- [Free tunnel has no SLA and can rate-limit] → Acceptable for a demo.
