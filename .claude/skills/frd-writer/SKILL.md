---
name: frd-writer
description: Write a Functional Requirements Document (FRD) in Markdown from the fixed template in this skill, turning a BRD (and, for an existing system, its code and workflow docs) into detailed, testable, module-wise functional requirements with use cases, validations, permissions and traceability. Use when the user asks for an FRD, functional requirements, functional specification, use cases, or runs /frd-writer.
---

# FRD Writer

Goal: a complete **Functional Requirements Document** using `templates/frd-template.md`. The FRD explains **how the
system behaves** for each module and screen (inputs, outputs, rules, validations, messages, permissions) in testable
terms. It sits between the BRD (business *what/why*) and the HLD/LLD (technical *how*): no class names, tables or
API design.

## Sources (use in this order)

| Source | Use for |
|---|---|
| BRD (`Requirements/BRD-*.md` or the BRD `.docx`, converted with `requirements-to-markdown`) | Objectives, roles, screens, business rules, acceptance criteria – every BRD item must be traced |
| Development plan (`Requirements/Screens Wise Feature Development Plan/`) | Screen elements and functional points |
| For an existing system – Application workflow docs (`TaskFlow Proj/TFWorkflow/App WF in MD/<NN-Module>/`) | Verified screens, functionalities, who can see/use them, permission keys, step-by-step flows, messages |
| For an existing system – permission catalogue (`.../Common/Security/PermissionCatalog.cs`) and `Artifacts/permission-audit/Excel/*.xlsx` | Role × permission matrix (§3.2) |
| For an existing system – frontend routes and validators (`frontend/src/app/modules/*/*.routes.ts`, `Dtos/**/Validators`) | Routes, field rules and real error messages |

Never invent behaviour. If the BRD and the implemented system differ, describe the intended behaviour the user
confirms; otherwise describe the implemented one and add the difference to §10 as an open issue.

## Workflow

1. Read `templates/frd-template.md`; keep its section order and headings.
2. Build the **module map** (§2.1) with a short code per module (AUTH, DASH, MGMT, TASK, COMM, RPT, PROF, SET, TFA, LEGAL …).
3. For **each module → child module → screen**, fill §4.x: screen table, elements, functional requirements, at least
   one use case for each main action, validations, business rules applied, notifications and messages.
4. Number requirements `FR-<CODE>-<nn>` (unique, sequential per module) and use cases `UC-<CODE>-<nn>`; give each FR a
   MoSCoW priority, the permission key that controls it, and its BRD trace.
5. Fill §3.2 from the permission catalogue (Granted / Denied / Always On / Configurable).
6. Fill cross-module workflows (§5) with a sequence diagram; reports (§6); the notification catalogue (§7).
7. Fill the traceability matrix (§11): every BRD screen requirement and business rule maps to ≥ 1 FRD requirement.
8. Header/footer: version, date `DD-MMM-YYYY`, author **Jojo George (jojogeorge3344@gmail.com)**, project type
   **Personal Project (Skill Development)**, copyright. Never call the project academic/college/MCA.
9. Save to `Requirements/FRD-<Project>.md` (or where the user says). Optional DOCX (docx skill/pandoc) and PDF (rendered
   Mermaid, copyright footer). For very large systems the user may prefer one file per module:
   `Requirements/FRD/<NN-Module>.md` with an index file – ask once if unclear.
10. **Check**:
    - No `{{` left (`grep -n "{{" <file>`).
    - IDs unique; every FR has priority, permission and trace; every BRD item appears in §11.
    - Every role in §3.1 appears in some screen's Access; every permission key used exists in the catalogue.
    - Mermaid blocks render (`mmdc -i x.mmd -o x.svg`).
11. **Report**: file path(s), counts of modules, screens, FRs, use cases, untraced BRD items (should be 0), open issues.

## Writing rules

- "The system shall …", one behaviour per requirement, measurable where possible.
- Quote real UI labels and error messages.
- Tables over prose; keep diagrams ≤ 12 nodes; no technical internals.
