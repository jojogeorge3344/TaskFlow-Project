---
name: lld-writer
description: Write a Low-Level Design (LLD) document in Markdown from the fixed template in this skill: per-module class/file inventories, class and sequence diagrams, full API contracts, service logic, repository methods, validation and error rules, database tables, indexes, enums, migrations, background jobs, configuration and tests. For an existing system it is derived from the code and verified against it. Use when the user asks for an LLD, low-level design, detailed design, class design or module design document, or runs /lld-writer.
---

# LLD Writer

Goal: a complete, **code-accurate Low-Level Design** using `templates/lld-template.md`. The LLD is detailed enough to
implement or maintain each module: real classes, files, endpoints, DTOs, rules, tables and tests. Every name must
exist in the code (for an existing system) – never invent.

## Sources

| Source | Use for |
|---|---|
| Code facts – `python "<tdd-sync-skill-dir>/scripts/extract_facts.py" "<workspace>/TaskFlow Proj" "<scratch>/facts.md"` (the `tdd-sync` skill) | Every endpoint with its authorization, permission keys, entities and properties, enums, migrations, services, repositories, config keys, routes, tests |
| Backend source `TaskFlow Proj/backend/src/TaskFlow.Api/` | Service logic, validators, exceptions, `AppDbContext` configuration (keys, indexes, FKs, triggers), background jobs |
| Frontend source `TaskFlow Proj/frontend/src/app/` | Pages, components, services, routes, guards |
| Technical module docs `TaskFlow Proj/TFTechnicalWorkflow/Tech WF in MD/<NN-Module>/` | Already-verified endpoint tables, sequences, rules and gaps per child module – reuse them |
| `Artifacts/TDD-TaskFlow.md` §6–§16 and Appendices | Data model, API, auth, business logic, reminder engine |
| FRD | Traceability (§11) |

## Workflow

1. Read `templates/lld-template.md`; keep section order and headings.
2. Run the fact extraction (above) and read the whole fact sheet.
3. Decide the layout: one file `Artifacts/LLD-<Project>.md`, or – for a large system – an index plus one file per
   module `Artifacts/LLD/<NN-Module>.md` (same template, §4 holding that module). Ask once if unclear; default to one
   file per module when there are more than ~6 modules.
4. For **each module → child module** fill §4: class/file inventory (real paths), class diagram, API contracts (every
   controller action of that module: method, full route, permission/policy, request, response, status codes), one
   example request/response per write endpoint (from the real DTOs), service logic steps, sequence diagram,
   repository methods, validation rules with real messages, error cases, state machine where one exists.
5. Fill §5 from the entities and `AppDbContext`: ER diagram (split if large), one table per entity with columns,
   types, nullability, keys, indexes, defaults; constraints/triggers; enums with all values; migrations.
6. Fill §6 (background jobs and algorithms), §7 (security), §8 (config keys; secrets as `<secret>`), §9 (tests from
   the test projects), §10 (known gaps), §11 (FRD → endpoint/class → test).
7. Header/footer: version, date `DD-MMM-YYYY`, author **Jojo George (jojogeorge3344@gmail.com)**, project type
   **Personal Project (Skill Development)**, copyright. Never call the project academic/college/MCA.
8. **Verify against the code** with the `tdd-sync` verifier (it works for any design document):
   ```
   python "<tdd-sync-skill-dir>/scripts/verify_tdd.py" "<scratch>/facts.md" "<LLD file>"
   ```
   (for per-module files, concatenate them first). Fix until it reports **CLEAN**: every route, permission key,
   entity and enum value present; no stale routes. Render every Mermaid block (`mmdc -i x.mmd -o x.svg`).
9. Optional PDF (rendered Mermaid, copyright footer, page numbers) and DOCX.
10. **Report**: file path(s), modules, endpoints documented (should equal the fact sheet total), tables, verifier
    result, "To be confirmed" items.

## Writing rules

- Real names only, in backticks; full routes (`/api/v1/...`, `{id:int}` written as `{id}`).
- Mermaid: `classDiagram`, `sequenceDiagram`, `erDiagram`, `stateDiagram-v2`; ≤ 14 nodes each; no semicolons in
  sequence messages; quote labels with special characters.
- Unconfirmable items: `> ⚠️ To be confirmed: …`.
- Never copy secrets (keys, passwords, connection strings with credentials).
