---
name: requirements-to-markdown
description: Convert requirement documents (DOCX or PDF) into a clean, faithful, well-structured Markdown requirements file, plus a short report of anything that could not be extracted reliably. Use whenever the user supplies a .docx or .pdf containing requirements, BRD, FRD, SRS, user stories, functional specs, or business rules and wants it as Markdown, "converted", "extracted", "normalised", or prepared as input for OpenSpec proposals/specs/design/tasks, even if they don't say "markdown". Does NOT generate OpenSpec artifacts.
---

# Requirements DOCX/PDF → Markdown

Goal: a Markdown file that loses **nothing** and invents **nothing**, so downstream work (OpenSpec proposals, specs, designs, tasks) can trust it. Accuracy and completeness beat prettiness. Never create OpenSpec artifacts here — stop at the Markdown and the report.

## Workflow

1. **Locate inputs.** One or more `.docx`/`.pdf` paths. (`.doc` → ask the user to save as `.docx`.)
2. **Raw extraction** (deterministic, gets text/tables/headers/footers/footnotes/comments/list numbering out of the file):
   ```
   python "<skill-dir>/scripts/extract.py" "<input>" "<scratch>/<name>.raw.md"
   ```
   It prints JSON with `flags`. Use the scratchpad/temp dir for the raw file, not the output folder. Stdlib only for DOCX; PDF uses `pdftotext` (falls back to `pypdf`).
3. **Read the raw file fully**, and for PDFs/ambiguous cases also look at the source (Read tool can open PDF pages as images) to resolve tables and columns. Don't trust the raw output blindly: script output is a draft.
4. **Write the final file** `<input-name>.md` next to the input (or where the user says), following the rules below.
5. **Verify completeness**: every table row, list item, number, date, threshold, and named term in the raw text must appear in the final file. Spot-check a few numbers/IDs against the source. If the raw flags mention images, tracked changes or comments, handle them as in "Flags".
6. **Report** (in chat, brief): per document — output path, anything unreliable (list below), count of review flags.

## Fidelity rules (why: downstream specs derive from this wording)

- Keep original terminology, requirement IDs, numbering (e.g. `3.2.1`, `FR-012`), modal verbs (shall/should/may/must) and conditions exactly. Don't paraphrase, "improve", merge, or deduplicate requirements.
- Don't infer missing values, fill blanks, resolve contradictions, or expand abbreviations unless the document does. Blank cells stay blank and are flagged if meaningful.
- Drop only pure presentation: colours, fonts, page layout, repeated page numbers, decorative lines. Keep headers/footers content that carries meaning (doc title, version, status, confidentiality, dates) once, in the metadata block.
- Reorganising is allowed only for readability **without changing meaning**: promote the doc's own headings into a logical hierarchy, group loose paragraphs under the nearest section. Keep the original order unless the document is clearly scrambled; if you reorder, say so in the report.
- Text you cannot place stays in an `## Unplaced Content` section at the end rather than being dropped.

## Output structure

```markdown
# <Document title>

## Document Metadata
- Source file / Version / Date / Authors / Status (only what the document states)

## <Section from document, original numbering kept>
...
## Unplaced Content        (only if needed)
## Extraction Review Notes (only if flags exist; see below)
```

- **Lists** stay lists, nesting preserved. Numbered requirements keep their original numbers as literal text.
- **Tables** → Markdown tables when cells are short and the grid is regular. Use a table with a header row; keep column names as in the source.
  - Merged cells: repeat the value so each row is self-contained, and note it.
  - Cells with long text, multiple paragraphs, or nested tables → use a structured list instead, one item per row, each field labelled with its column header:
    ```
    - **Rule BR-04**
      - Condition: …
      - Action: …
    ```
  - Never separate a value from its column/row header.
- Diagrams/images that carry meaning: write `[Image: <filename or page>: content not extracted]` at the position, and flag. Describe only if you can actually see it, and mark it `(described by extractor, verify)`.
- Code, field names, and literals in backticks.

## Flags (never silently omit)

Mark in place with a blockquote and collect in `## Extraction Review Notes` using stable IDs `R1, R2…`:

> ⚠ REVIEW R3: <what is wrong, where (page/section), what the options are>

Flag at least: unreadable/garbled/scanned text; images or charts with possible content; ambiguous tables (unclear headers, merged-cell guesses, column alignment guessed in PDF); conflicting statements (quote both, with locations, do not pick one); ambiguous wording ("etc.", "TBD", "as appropriate", dangling references to missing sections/appendices); tracked changes or reviewer comments (unresolved decisions); duplicated IDs or numbering gaps; content you reordered or relocated.

## Report format (chat, brief)

```
<file>.docx → <path>.md
Not reliably extracted:
- R1 p.4 image: flow diagram, content not captured
- R2 §5.3 table: merged header, assumed columns
Conflicts: R5 §2.1 vs §7.4 on retry count (3 vs 5)
```
If nothing is wrong, say so explicitly. Do not offer or start generating OpenSpec artifacts.

## Notes on the script

- DOCX: preserves heading styles (Heading N → `#`×(N+1), H1 reserved for the title), real list numbering labels, tables with merge repetition, text boxes, content controls, headers/footers/footnotes/endnotes/comments. Tracked insertions are kept and deletions dropped (flagged).
- PDF: layout text only. Tables come out as column-aligned text; rebuild them yourself and flag uncertain alignments. Pages with almost no text are flagged as likely scanned (OCR not built in; read the page image yourself and mark the content `(read from image, verify)`).
