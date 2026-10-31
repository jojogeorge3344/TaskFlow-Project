---
name: tdd-sync
description: Update the TaskFlow Technical Design Document (TF - Documents/TF - TDD/TDD-TaskFlow.md) so it matches the real code, by extracting facts from the backend, frontend and tests and rewriting each TDD section from them. Use when the user asks to update, refresh, sync, rewrite or regenerate the TDD, technical design document or design doc "based on the project/code", or after features were added and the TDD is out of date.
---

# TaskFlow TDD Sync

Goal: an **accurate, current** `TF - Documents/TF - TDD/TDD-TaskFlow.md` that describes what TaskFlow actually is today,
while keeping traceability to the approved BRD set (`TF - Documents/TF - BRD/`, signed off 31-Oct-2026), the FRD, HLD
and LLD sets, and the 20 screen specs (the original BRD docx is retired and no longer in the repository). The code is the source of
truth for *how*; the approved BRD set is the source of truth for *what was asked*. Where they differ, the TDD documents the code and
records the difference in a **Deviations from BRD** section. Never invent an endpoint, table, permission or setting.

## Paths (workspace root = the folder containing `TaskFlow Proj/`)

| What | Path |
|---|---|
| Current TDD (input and output) | `TF - Documents/TF - TDD/TDD-TaskFlow.md` (+ `.pdf`) |
| Backend | `TaskFlow Proj/backend/src/TaskFlow.Api/` (Controllers, Services, Repositories, Dtos, Models, Integrations, Authorization, Middleware, Filters, Config, Common, Persistence) |
| Tests | `TaskFlow Proj/backend/tests/` (TaskFlow.UnitTests, TaskFlow.IntegrationTests) |
| Frontend | `TaskFlow Proj/frontend/src/app/` (modules/, shared/, layouts/, routes/, config/) |
| Permissions (source of truth) | `.../Common/Security/PermissionKeys.cs`, `PermissionCatalog.cs`, `SystemRoles.cs` |
| Module docs (rich, already verified) | `TaskFlow Proj/TFTechnicalWorkflow/Tech WF in MD/<NN-Module>/` and `TaskFlow Proj/TFWorkflow/App WF in MD/` |
| Requirements | Approved BRD set `TF - Documents/TF - BRD/` (start with `00-Overall/BRD-TaskFlow-Overall.md`: acceptance criteria `AC-01 – AC-26` in §14); screen specs `TF - Documents/Screens Wise Feature Development Plan/01..20-*.md` |
| Design document set | `TF - Documents/TF - FRD/`, `TF - HLD/`, `TF - LLD/` (00-Overall + 16 sub-modules each; the HLD/LLD cite TDD sections by number) |
| Other context | `README.md`, `TaskFlow Proj/project.md`, `TaskFlow Proj/agent.md`, permission documents `TF - Documents/Permission Requirement/` |

## Workflow

1. **Back up** the current TDD: copy it to `TF - Documents/TF - TDD/TDD-TaskFlow.v<old-version>.md` (e.g. `v2.1`). Never lose the old one.
2. **Extract facts** (deterministic, read-only):
   ```
   python "<skill-dir>/scripts/extract_facts.py" "<workspace>/TaskFlow Proj" "<scratch>/tdd-facts.md"
   ```
   It lists: target framework and packages, every controller action (verb, full route, authorization attributes),
   permission keys, DbSets and entity properties, enums, migrations, appsettings keys (secrets omitted), services,
   repositories, integrations, hosted services, frontend routes and menu, and test files. Read the whole fact sheet.
3. **Read** the current TDD fully, then the module docs' overview files (`NN.0-*-Overview.md`) and the technical child
   docs for any area you rewrite. Read `PermissionCatalog.cs` for role defaults (Always On / Granted / Denied /
   Configurable) and assignability.
4. **Rewrite section by section** following `references/section-map.md` (what each section must contain and where its
   facts come from). Keep the existing section numbering where possible so BRD references stay valid; add new sections
   for capabilities the original design did not have (roles and permissions, MFA, password recovery, internal mail,
   projects and positions, the TFA assistant, legal pages, data folder and key ring). **Never renumber an existing
   section** – the HLD and LLD sets cite TDD sections by number.
   Keep §1.5 Reference Documents and §1.6 Document Hierarchy & Traceability current: list the folders under
   `TF - Documents/` and confirm every path you name exists, update document versions and statuses (read the Document
   Control block of each `*-TaskFlow-Overall.md`), and keep the sub-module map (17 rows) and the §21 mapping to the
   approved BRD acceptance IDs complete.
5. **Header and history**: bump the version (1.0 → 2.0 for a full sync, x.1 for a partial one), set Status to
   "Updated to match implementation", Date to today, and add a **Revision History** table right after the header
   (version, date, author, summary). Author: Jojo George (jojogeorge3344@gmail.com). Keep the copyright line
   `© <year> Jojo George. All rights reserved.` at the top and as the last line.
6. **Write** the new `TF - Documents/TF - TDD/TDD-TaskFlow.md`. For large rewrites, write section by section (outline first), not in
   one pass.
7. **Verify**:
   ```
   python "<skill-dir>/scripts/verify_tdd.py" "<scratch>/tdd-facts.md" "<workspace>/TF - Documents/TF - TDD/TDD-TaskFlow.md"
   ```
   It reports endpoints, permission keys, entities and enums from the code that the TDD does not mention, and routes in
   the TDD that do not exist in the code. Fix every reported item (or list it on purpose under Deviations), then rerun
   until clean. Also render every Mermaid block (`mmdc -i x.mmd -o x.svg`) and fix failures.
   Then check document references: every path in backticks that starts with `TF - Documents/`, `TaskFlow Proj/`,
   `openspec/` or `.claude/` must exist; no `Artifacts/` or `Requirements/` paths remain.
8. **Optional PDF**: if the user wants one, convert with the same MD→PDF approach used for the module docs (Mermaid
   rendered to SVG, copyright footer, page numbers) and save it next to the MD as `TDD-TaskFlow.pdf`.
9. **Report** briefly: backup path, new version, sections changed or added, number of endpoints/permissions/entities
   covered, verification result, and the main deviations from the BRD.

## Writing rules

- Describe the **current code**, in present tense. Old design ideas that were dropped or replaced go to
  **Deviations from BRD** or **Superseded design** with one line each: what changed and why if known.
- Endpoint tables: real routes from the fact sheet (`/api/v1/...`), the verb, the permission key or policy, and the
  purpose. Group by module in the same order as the module docs (Auth, Dashboard, Management, Monitoring,
  Communication, Reports, Profile, Settings, Assistant, Legal).
- Data model: one table per entity with columns from the entity class; keys, unique indexes and FKs from
  `AppDbContext`/configurations; enums from `Models/Enums`. Update the Mermaid ER diagram to the real entities.
- Permissions: list keys by module with Administrator / Manager / Employee / Custom role defaults from
  `PermissionCatalog.cs` (not from memory, not from the audit CSV when they disagree).
- Configuration: key names and defaults from `appsettings.json`; **never copy secrets** (keys, passwords, connection
  strings with credentials, tokens). Write `<secret>`.
- If the TDD changes a section the sub-module HLD/LLD cite, say in the report which sub-module documents need the
  same update (use the §1.6 map).
- Keep BRD traceability tables (screen → route → service → endpoint; acceptance criteria → section) and update them
  to real names.
- Use tables over prose; keep diagrams small and valid Mermaid. No bare `<Tag>` text outside backticks.
- Anything you cannot confirm from code: `> ⚠️ To be confirmed: …`. Do not guess.
