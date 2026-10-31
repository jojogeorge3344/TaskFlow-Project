---
name: module-docs
description: Generate module-wise functional workflow docs (TFWorkflow) and technical docs (TFTechnicalWorkflow) for the TaskFlow project as one Markdown file per module, using the fixed templates in this skill. Use whenever the user asks for module-wise functionality, screens, purpose, who can see/use a feature, required permissions, workflow documentation, technical explanation, API/endpoint/architecture docs per module, or runs /module-docs, even if they name only one module.
---

# TaskFlow Module Documentation (Workflow + Technical)

Goal: for every TaskFlow module, produce **two** Markdown files with the same name:

| Doc | Audience | Template | Output folder |
|-----|----------|----------|---------------|
| Functional workflow | Guide, examiner, end users (non-technical) | `templates/workflow-template.md` | `TaskFlow Proj/TFWorkflow/` |
| Technical | Developers | `templates/technical-template.md` | `TaskFlow Proj/TFTechnicalWorkflow/` |

Everything written must come from the real project (code, requirements, permission catalog). **Never invent** endpoints, permission keys, screens, fields or rules. If something cannot be confirmed, write `> ⚠️ To be confirmed:` with what is missing.

## Module → File Mapping

One file per module; child modules are sections (`## 5. Child Module: …`) inside it.

| File | Module | Child modules |
|------|--------|---------------|
| `01-Auth.md` | Auth | Login, Password Recovery, MFA / 2FA, Sessions |
| `02-Dashboard.md` | Dashboard | Overview (Admin / Manager / Employee), Analytics |
| `03-Management.md` | Management | Employees, Departments, Positions, Projects |
| `04-Monitoring.md` | Monitoring | Tasks (List, Create, Details, Kanban, Reassignment), Comments, Activity History, Workload |
| `05-Communication.md` | Communication | Notifications, Mail |
| `06-Reports.md` | Reports | Reports |
| `07-Profile.md` | Profile | User Profile |
| `08-Settings.md` | Settings | Application Settings, Reminder Settings, Email Notification Settings, Roles & Permissions, AI Providers, 2FA Settings |
| `09-Assistant.md` | Assistant | Chat, Actions / Tools, Audit Log |

If the code shows a module or child module not in this table, add it and mention it in the report.

## Sources (read in this order)

1. **Screens & functional rules**: `TF - Documents/Screens Wise Feature Development Plan/NN-*.md` (20 screen files) and the approved BRD set `TF - Documents/TF - BRD/`.
2. **Permissions & roles**: the permission documents in `TF - Documents/Permission Requirement/` (Excel and PDF, all roles and per role). The `PermissionCatalog.cs` file in the backend is the source of truth when they disagree.
3. **Backend** (`TaskFlow Proj/backend/src/TaskFlow.Api/`):
   - `Controllers/<Module>/*Controller.cs`: routes, HTTP verbs, `[Authorize]` policies
   - `Services/<Module>/<Child>/`: business logic and rules
   - `Repositories/<Module>/<Child>/`: queries
   - `Dtos/<Module>/<Child>/` and `Dtos/<Module>/Validators/`: request/response and validation
   - `Models/Entities`, `Models/Enums`, `Authorization/`, `Common/`, `Integrations/`
4. **Frontend** (`TaskFlow Proj/frontend/src/`): module folders, routes, guards, API services, permission directives.
5. **Overall design**: `TF - Documents/TF - TDD/TDD-TaskFlow.md`, `TaskFlow Proj/project.md`.

## Workflow

1. **Confirm scope**: all modules, or the ones the user named.
2. **Per module, gather facts** from the sources above. Build a working list of: screens, functionalities, permission key per functionality, role access (Administrator / Manager / Employee / Custom Role), scope (Own / Team / Organization), endpoints, DTOs, entities, validations, side effects (notifications, mail, activity log).
3. **Write the workflow doc** by filling `templates/workflow-template.md`:
   - Plain, non-technical language. No class names or code in this file except permission keys in backticks.
   - Every functionality row must name **purpose, who can see, who can use, and the permission key**.
   - Role access values must follow the permission matrix wording: `Always On`, `Granted`, `Denied`, `Configurable`.
4. **Write the technical doc** by filling `templates/technical-template.md`:
   - Follow the Controller → Service → Repository flow.
   - Endpoints, policies and file paths must match the code exactly.
   - Mermaid diagrams must be valid (flowchart / sequenceDiagram).
5. **Save** both files with the same file name in their folders (see the table above). Overwrite only after telling the user if a file already exists.
6. **Verify**:
   - Every screen in the requirement files for this module appears in the workflow doc.
   - Every controller action appears in the technical doc's endpoint table.
   - Every permission key used is found in the permission catalog / matrix.
   - The workflow doc and technical doc for the same module use the same permission keys.
7. **Report** (brief, in chat): files written, any `To be confirmed` items, and mismatches found between requirements, permissions and code.

## Writing Rules

- Keep the template section order and headings. Remove a section only if it truly doesn't apply, and replace it with `_Not applicable for this module._`.
- Tables over long paragraphs. Short, clear sentences.
- Dates as `DD-MMM-YYYY`. Version starts at `1.0`.
- Every file keeps the header block with: **Author: Jojo George** and **© <year> Jojo George. All rights reserved.**
- Do not copy secrets, connection strings or API keys from config files into the docs.
