---
name: hld-writer
description: Write a High-Level Design (HLD) document in Markdown from the fixed template in this skill: system context, architecture, modules and their interactions, data domains, integrations, security, technology stack, deployment and design decisions, with Mermaid diagrams. For an existing system it derives the content from the code. Use when the user asks for an HLD, high-level design, architecture document or architecture overview, or runs /hld-writer.
---

# HLD Writer

Goal: a complete **High-Level Design** using `templates/hld-template.md`. The HLD shows the **big picture**: what the
parts of the system are, what each is responsible for, how they talk to each other and to the outside world, and
where it all runs. It does **not** list classes, methods, columns or every endpoint – those belong in the LLD.

## Sources

| Source | Use for |
|---|---|
| BRD / FRD | Scope, actors, quality goals, traceability (§15) |
| Code facts – run `python "<tdd-sync-skill-dir>/scripts/extract_facts.py" "<workspace>/TaskFlow Proj" "<scratch>/facts.md"` (the `tdd-sync` skill) | Real packages and versions, modules, hosted services, integrations, data stores, routes |
| `Artifacts/TDD-TaskFlow.md` §1–§5, §14–§19 | Architecture, decisions, security, configuration, deployment, backup |
| Module overview docs (`TaskFlow Proj/TFTechnicalWorkflow/Tech WF in MD/<NN-Module>/NN.0-*-Overview.md`) | Module responsibilities and dependencies |
| `README.md` | Data folder, setup, technologies |

For a new system with no code yet, design from the BRD/FRD and mark choices as proposed.

## Workflow

1. Read `templates/hld-template.md`; keep the section order and headings.
2. Extract code facts (above) when code exists; read the TDD sections listed.
3. Fill every section. Diagrams:
   - Context (§2.1), logical architecture (§4.2), module interaction (§5.1), data domains (§6.2, entity names only,
     no columns), one sequence diagram per key flow (§8: sign-in, main business flow, background job, one integration),
     deployment (§11).
   - Keep each Mermaid diagram ≤ 14 nodes; quote labels with special characters; no semicolons in sequence messages.
4. Module table (§5): one row per module with responsibility, *high-level* components and dependencies. No class lists.
5. Technology table (§10): real versions from the fact sheet; never list tools that are not installed.
6. Design decisions (§13): each with alternatives and rationale; risks and open issues (§14).
7. Traceability (§15): main BRD/FRD items → module/component.
8. Header/footer: version, date `DD-MMM-YYYY`, author **Jojo George (jojogeorge3344@gmail.com)**, project type
   **Personal Project (Skill Development)**, copyright. Never call the project academic/college/MCA.
9. Save to `Artifacts/HLD-<Project>.md`. Optional PDF (rendered Mermaid, copyright footer) and DOCX.
10. **Check**:
    - No `{{` left; every module in the code appears in §5; versions match the fact sheet.
    - No low-level detail leaked (no column lists, method names, full endpoint tables).
    - All Mermaid blocks render (`mmdc -i x.mmd -o x.svg`).
11. **Report**: file path, sections, number of diagrams, decisions recorded, open issues.

## Writing rules

- Describe responsibilities and interactions, not implementation.
- Tables and diagrams over prose; present tense for an existing system.
- Secrets never appear (write `<secret>`).
