---
name: brd-writer
description: Write a Business Requirements Document (BRD) in Markdown from the fixed template in this skill, for a new project or module, or rewrite an existing BRD into this format. Use whenever the user asks for a BRD, business requirements, requirement document, "requirements for <project>", screen-wise requirements, or runs /brd-writer, even if they don't say "template".
---

# BRD Writer

Goal: a complete, consistent **Business Requirements Document** in Markdown, using `templates/brd-template.md`. A BRD says
**what** the business needs and **why**, in plain language a non-developer can approve. It does not design the
solution: architecture, APIs, database design and code belong in the TDD/HLD/LLD.

## Inputs to gather first

Ask only for what you cannot find. If the user is not present, use the most reasonable answer and list it under
"Assumptions" (section 12).

1. Project title and one-sentence purpose.
2. The business problem today (who suffers, what goes wrong).
3. User roles and what each may do.
4. Modules and screens (or the features, from which you derive screens).
5. Main workflows (e.g. task creation → reminders → completion).
6. Must-have rules (permissions, validations, limits, deadlines).
7. Anything explicitly out of scope.
8. Existing material: an older BRD (`.docx`/`.pdf` → convert first with the `requirements-to-markdown` skill),
   screen notes, emails, the existing app.

## Workflow

1. **Read** `templates/brd-template.md` fully. Keep its section order and headings.
2. **Collect** inputs (above). From existing code or docs, extract facts; never invent features the user did not ask for.
3. **Fill** every section. Replace each `{{placeholder}}` with real content; delete placeholder text and the "Repeat this
   block" notes. If a section truly does not apply, keep the heading and write `_Not applicable for this project._`.
4. **Number requirements** so they are traceable:
   - Objectives `OBJ-01…`, business rules `BR-1…`, functional requirements `FR-<screen>.<n>` (e.g. `FR-8.3`),
     non-functional `NFR-1…`, risks `R-1…`, acceptance criteria `AC-1…`.
   - Write requirements as testable "The system shall …" statements, one behaviour per line.
   - Give each functional requirement a priority: **Must / Should / Could** (MoSCoW).
5. **Screens**: one `SCREEN n` block per screen, in user-journey order (login → dashboards → main work → settings →
   profile). Every screen has name, module, access, priority, purpose, elements, functional requirements,
   validations & messages, expected result.
6. **Workflows**: a step table per workflow, plus a small Mermaid flowchart (≤ 12 nodes, labels in quotes) and one
   worked example with realistic values.
7. **Traceability**: fill section 15 so every objective links to rules, screens, FRs and an acceptance criterion.
   Every `FR` must appear in at least one row; every `AC` must point back to an FR.
8. **Header**: version `1.0` (bump for revisions and add a Revision History row), status, today's date
   (`DD-MMM-YYYY`), author **Jojo George (jojogeorge3344@gmail.com)**, project type **Personal Project (Skill
   Development)**, copyright line at the top and the `<sub>` footer at the bottom. Never describe the project as an
   academic, college or MCA project.
9. **Save** as `TF - Documents/TF - BRD/00-Overall/BRD-<ProjectName>-Overall.md`, or for a sub-module
   `TF - Documents/TF - BRD/<NN-Module>/<NN.N-Sub-Module>/BRD-<Sub-Module>.md` (or where the user says). If the user also wants Word or PDF, convert
   the finished Markdown (DOCX via the `docx` skill or pandoc; PDF with rendered Mermaid diagrams and the copyright
   footer).
10. **Check before finishing**:
    - No `{{` placeholders remain (`grep -n "{{" <file>`).
    - IDs are unique and sequential; every FR is in the traceability matrix; every AC references an FR.
    - Every role in 2.2 appears in at least one screen's Access.
    - Mermaid blocks render (`mmdc -i x.mmd -o x.svg`).
    - No technical design leaked in (no endpoints, table schemas or class names) except the optional section 11.
11. **Report** briefly: file path, number of screens, FRs, BRs, NFRs, ACs, and the assumptions you made.

## Writing rules

- Plain business language; short sentences; tables over paragraphs.
- Be specific and measurable ("within 2 seconds", "maximum 5 files of 10 MB"), not vague ("fast", "user-friendly").
- One requirement per row; no "and/or" bundles.
- Use the user's own terms for roles, modules and screens consistently throughout.
- Keep the technology stack as a *recommendation* only.
