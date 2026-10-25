# Design

## Context

See `proposal.md` for motivation. Relevant current state:

- **Aggregation conventions**: `DashboardService` reads the `Task`/`User`/`Department` tables with `IAppClock` for timezone-aware day boundaries, and defines open/overdue as "not Completed and not Cancelled" and "open and past deadline". The Admin and Manager dashboards count **organization-wide** active tasks (they are not narrowed to a single manager's own tasks).
- **Frontend visuals**: dashboards render stat cards plus a CSS progress bar; there is no charting library. Styling uses the `tf-` primitives in `styles.css`.
- **Auth policies**: `AdminOnly`, `ManagerOrAdmin`, `TaskAccess`.
- **Task model**: `Task` carries `AssignedToUserId`, `CreatedByUserId`, optional `DepartmentId`, `Priority`, `Status`, `StartDate`, `Deadline`, `EstimatedHours`, `IsDeleted`.

## Goals / Non-Goals

**Goals:**
- Add a per-employee workload aggregation and a filtered, exportable task report, both Administrator/Manager-only.
- Keep the reported figures consistent with the dashboards and reminders (shared open/overdue rules, shared `IAppClock` timezone).
- Reuse existing styling and filter conventions rather than adding dependencies.

**Non-Goals:**
- Introducing a charting library.
- Changing the existing dashboards or their scoping.
- Per-user report scheduling, other export formats, or historical report snapshots.

## Decisions

### 1. Dedicated `WorkloadService` and `ReportService`, not an extension of `DashboardService`
The dashboard returns a fixed set of counters; these screens need filterable aggregation and CSV export, which are different shapes and concerns. Separate read-only services/controllers keep each responsibility small.
*Alternatives:* growing `DashboardService` (rejected — mixes fixed counters with filtered aggregation and export).

### 2. Endpoints behind `ManagerOrAdmin`
`GET /api/v1/workload`, `GET /api/v1/reports` and `GET /api/v1/reports/export` on `WorkloadController` and `ReportController`, all `[Authorize(Policy = ManagerOrAdmin)]`. Employees receive 403, matching the screens' role guards.

### 3. Organization-wide scope, matching the existing dashboards
The Admin and Manager dashboards already count org-wide active tasks, so workload and reports deliberately follow the same organization-wide convention for Manager/Admin rather than inventing per-manager narrowing that the rest of the app does not have. This keeps figures consistent across screens; it is recorded here so a future request to truly scope by manager is a known, deliberate change.

### 4. Workload includes every active user, with zero-load rows
The aggregation left-joins the task counts onto all active users so employees carrying no tasks still appear — that is the information a manager needs when reallocating work. Counts: **active** (open), **high-priority** (open and `High`/`Critical`), **overdue** (open and past deadline), **completed**. The department comes from the user's department. Overdue uses `IAppClock` so it agrees with dashboards and reminders.

### 5. "Unusually high" highlight is a client-side rule
The workload component computes the average active count across the rows it rendered and highlights rows at or above twice that average (skipped when the average is zero). Keeping it client-side avoids a second round-trip and keeps the rule tweakable without an API change.

### 6. Reports date range filters on the task deadline (inclusive)
`from`/`to` filter `Task.Deadline` inclusively when supplied. The requirement does not name a field; deadline is the most useful axis for overdue/completion analysis and is applied consistently to both the totals and the CSV export.

### 7. Report totals use the shared status semantics
Within the filtered scope: **total** = all non-deleted matching tasks, **pending** = open, **completed** = Completed, **overdue** = open and past deadline. Cancelled tasks count toward total but not pending, mirroring the dashboards.

### 8. CSV is streamed as a download with formula-injection protection
`GET /api/v1/reports/export` returns `text/csv` with a `Content-Disposition` attachment, columns `TaskCode, Title, Assignee, Department, Priority, Status, StartDate, Deadline, EstimatedHours, CompletedAt`. Cell values beginning with `=`, `+`, `-` or `@` are prefixed with a single quote to prevent spreadsheet formula injection.
*Alternatives:* returning JSON with an `export=true` flag (rejected — a real CSV content-type is simpler for the client).

### 9. Frontend reuses existing primitives and filter conventions
Both screens are children of the shell with `roleGuard(['Administrator','Manager'])`, use `tf-page-header`/`tf-toolbar`/`tf-table`, and render the completion summary and workload bars as CSS bars like the dashboard progress bar. CSV download uses an `HttpClient` blob plus a temporary anchor. Services mirror `DashboardService`/`TaskService` style.

## Risks / Trade-offs

- **Organization-wide scope may surprise users expecting per-manager data** → documented as decision 3 and consistent with the existing dashboards; a genuine scoping change is future work.
- **CSV formula injection from task titles** → mitigated by the single-quote prefix on leading formula characters (decision 8).
- **Date-range field choice (deadline) is an assumption** → documented in decision 6 and applied identically to totals and export; easy to switch later.
- **Aggregation cost at large scale** → single grouped queries over indexed task tables; fine for the stated office workload.

## Migration Plan

No schema change — both capabilities aggregate existing data. Deploy backend and frontend together; the frontend degrades to empty states if the endpoints fail. Rollback is a normal redeploy with no data or schema reversal.

## Open Questions

None that change the specs, approach or task breakdown.
