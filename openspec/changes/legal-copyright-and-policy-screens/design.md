# Design

## Context

See `proposal.md` — Why for the motivation.

The constraints that shape this design, all verified against the current code:

- **`app.routes.ts` has exactly one public route.** `login` sits at the top level; every other screen is a child of the shell route behind `authGuard`. A `**` catch-all at the end of the array redirects everything unmatched to `login`.
- **The shell's content column is capped.** `.content__inner` is `max-width: 80rem`, centred, inside `.content` which carries the left margin that clears the fixed sidebar. Anything placed inside `.content__inner` therefore inherits the reading width of the page above it.
- **The login screen is a dark, flex-centred column.** `.login` is `min-height: 100vh`, `align-items: center`, `justify-content: center` over a `#0f172a` background, with a 22rem white card. A footer placed beside that card would sit next to it, not below it.
- **There is no global chrome component.** `AppComponent` renders only `<router-outlet>`; the shell is a route-level component. The TFA panel and the toast host are siblings of the shell at the bottom of `shell.component.html`.
- **The design system is already sufficient.** `styles.css` carries the colour, radius, spacing and shadow tokens and the `.tf-page`, `.tf-page-header`, `.tf-card`, `.tf-btn` primitives. Icons are inline SVG paths in `core/layout/icon.component.ts` — there is no icon font and no CDN dependency to attribute.
- **The legal content must be accurate to the implementation.** The claims the Privacy Policy makes are traceable to specific code: PBKDF2 hashing via ASP.NET Core's `PasswordHasher<User>` (`Program.cs:138`), local storage under the TaskFlow data folder, administrator exclusion from private mail, and the cloud-AI and browser-speech exceptions documented in the README.

## Goals / Non-Goals

**Goals:**

- Five legal documents served from one renderer, so layout, table of contents, accessibility and print rules are written once and cannot drift between documents.
- The documents reachable without a session, and reachable from both the sign-in screen and every authenticated screen.
- One definition of the owner, the year and the contact address, used by the documents, the footer and the published page metadata.
- Every factual claim traceable to the implementation rather than to boilerplate.

**Non-Goals:**

- Any backend surface. No endpoint, no table, no migration. The documents are static content served by the frontend.
- A terms-acceptance step that gates sign-in, or any record of who accepted what.
- Making the documents editable by an Administrator at runtime.
- Legal advice. The documents state the position the software actually takes; they are not a substitute for review by a qualified lawyer before any real-world deployment.
- Numbered screen documents under `Requirements/screens wise feature/`, and any change to the existing `app-shell` requirements.

## Decisions

### D1 — Legal content lives in a typed registry, not in templates

The prose for all five documents lives in one TypeScript module as structured data: an array of documents, each with sections, each with blocks. A block is a paragraph, a list, a table or a callout.

**Why:** the prose is the artefact a reviewer reads. Keeping it in one file makes it reviewable in one pass and makes the content impossible to break structurally — a table row count, a heading level or a table of contents entry is derived from the data rather than hand-written five times.

**Alternatives considered.** *Five separate components with their own templates* — rejected: duplicates the layout, the table of contents and the accessibility work five times over, and the documents would drift apart in styling. *Markdown files fetched at runtime and rendered* — rejected: needs a markdown dependency the project does not have, and the `**` catch-all plus `base href` routing makes fetching static assets an extra failure mode for no benefit. *Content served by the backend* — rejected: no backend change is warranted for static text, and it would put a database row in front of every legal statement.

### D2 — One page component, selected by route data

A single `LegalPageComponent` renders whichever document the route names. Each route carries the document id in `data`, and the component reads it from the router.

```
/legal/copyright  ─┐
/legal/terms      ─┼─> LegalPageComponent ──> legal-documents.ts ──> LegalDocument
/legal/privacy    ─┤        (route data)          (registry)          (sections -> blocks)
/legal/disclaimer  ─┤
/legal/licenses   ─┘
```

**Why:** one component means one set of layout rules and one spec file, and adding a sixth document later is a registry entry plus a route rather than a new component.

**Alternatives considered.** *Five routes pointing at five components* — rejected: five copies of the same layout. *A single route with the document id as a path parameter* (`/legal/:documentId`) — rejected: it hides the five documents behind one URL, so the addresses a reviewer writes down, and the footer links, would not name them.

### D3 — The legal routes are top-level and public

The five routes are registered in `app.routes.ts` as siblings of `login`, outside the `authGuard` shell route, and ahead of the `**` catch-all.

**Why:** a legal notice that needs a password is not a notice. A reviewer opening the project, a visitor, or a user who has not yet created an account can all read the terms, the privacy policy and the copyright notice. Registering them outside the shell also means they need no permission catalog entry and no role plumbing — the `permissionGuard` catalogue has no legal permission to grant.

**Alternatives considered.** *Children of the shell route* — rejected: they would need an account to read and would appear in the signed-in chrome. *A single `/legal` index that links to the five* — rejected: adds a hop, and the requirement is that each document has its own address.

### D4 — The footer is one component with two visual variants

`LegalFooterComponent` takes a `variant` input of `surface` (light, for the app shell) or `dark` (light text, for the sign-in screen and the legal pages). It renders the ownership line and the five document links, and marks the current document with `aria-current="page"`.

```
+------------------------------------------------------------------+
| (c) 2026 Jojo George. All rights reserved.                        |
| TaskFlow - Intelligent Task Allocation & Tracking System.        |
| Copyright  Terms of Use  Privacy Policy  Disclaimer  Licences    |
+------------------------------------------------------------------+
```

**Why:** the same ownership statement has to sit on a dark navy sign-in background and on the light application background. Two variants of one component keep the wording and the link set identical in both places — a duplicated footer would be free to disagree with itself.

**Placement.** In the shell, the footer goes inside `.content__inner` after `<router-outlet />`, so it inherits the 80rem reading column and aligns with the content above it. On the sign-in screen, `.login` changes to `flex-direction: column` with a gap, so the card and the footer stack and centre together rather than sitting side by side.

**Alternatives considered.** *A footer in `AppComponent`, outside the shell* — rejected: the shell renders inside a route component, so a sibling of the router outlet would not sit beneath the shell's content area. *A CSS-only pseudo-element footer* — rejected: the links would not be focusable or readable by assistive technology.

### D5 — Legal text renders as structured blocks, never as raw HTML

Blocks are rendered with Angular's own control flow and interpolation — `@for`, `@if`, `{{ }}` — from typed data. No `[innerHTML]`, no `DomSanitizer` bypass.

**Why:** `icon.component.ts` already uses `DomSanitizer` for a fixed, trusted constant. Legal prose is larger and would be edited often; rendering it through `innerHTML` would open a stored-XSS path into every page of the application for no benefit. Structured blocks also give the renderer a place to hang semantics — a `list` block renders as a real `<ul>`, a `table` block as a real `<table>` with a caption and header cells.

**Alternatives considered.** *A single HTML string per section, sanitised* — rejected: sanitisation is a runtime tax and a recurring risk for content that is entirely ours.

### D6 — The owner, the year and the address are defined once

A single `LEGAL_OWNER` constant carries the owner name, the year, the contact address, the application name and the rights statement. The documents, the footer and the published page metadata all read from it.

```ts
export const LEGAL_OWNER = {
  name: 'Jojo George',
  year: 2026,
  email: 'jojogeorge3344@gmail.com',
  applicationName: 'TaskFlow',
  applicationTitle: 'Intelligent Task Allocation & Tracking System',
  rights: 'All Rights Reserved'
} as const;
```

**Why:** the requirement that these cannot drift apart is only enforceable if there is one place to change them. The metadata is applied from the same constant at runtime, so the running application has exactly one source.

**The one duplication, and its guard.** `index.html` carries the same values statically so the head is correct before Angular boots and if scripting is unavailable. That file cannot import a TypeScript constant, so the values appear twice by necessity. A frontend spec asserts that the static values in `index.html` match `LEGAL_OWNER`, which turns a silent drift into a failing test.

### D7 — Legal pages get their own reading layout

The legal pages are not rendered inside the application shell. They carry a minimal chrome: the TF mark and the application name, the document title, a "Back to sign in" link, the document body with a generated table of contents, and the footer.

**Why:** a public page should not pretend the reader is signed in, and the shell's sidebar and account area would be misleading to a visitor. The body is set to a comfortable measure with generous line height, because these documents are read rather than scanned.

**Alternatives considered.** *Reusing the shell layout* — rejected: it exposes navigation for screens the visitor cannot open. *A bare document with no chrome* — rejected: the reader loses the application identity and the way back.

### D8 — Print rules are scoped to the legal pages

Print styles live in the legal page component's own stylesheet, inside `@media print`, and are limited to colour, page-break control and the hiding of screen-only furniture.

**Why:** a legal document is something a person may genuinely need to print or save as a PDF — for a submission, a review, or an offline copy. Scoping the rules to the component keeps them from leaking into the application's own screens.

### D9 — No new dependencies

The change uses only Angular Router, Angular's control flow, the existing `Title` and `Meta` services, and the existing design tokens.

**Why:** the project has no markdown, no sanitiser and no markdown-to-HTML dependency, and adding one for static prose the author controls would be the largest dependency in the project for the smallest benefit.

## Risks / Trade-offs

- **A published claim drifts from the implementation** → the Privacy Policy's factual claims are written against specific verified sources (the password hasher registration, the storage path, the mail permission checks, the AI provider and speech behaviour). Each claim gets its own scenario, and the document task lists the claims that must be re-checked against the code whenever the underlying behaviour changes.
- **The privacy policy promises something the system does not do** → the two exceptions the implementation genuinely has — cloud AI providers and browser speech recognition — are stated prominently in the policy rather than buried, and the disclaimer repeats the point for assistant output.
- **The footer changes the shell's layout** → the footer is placed inside `.content__inner` after the router outlet, which adds to the scroll height rather than to any fixed or flex container, so it cannot disturb the header, the sidebar or the content width. The existing `app-shell` requirements are re-checked against the result.
- **`index.html` drifts from `LEGAL_OWNER`** → a spec asserts the static head values match the constant.
- **The footer sits awkwardly on very short pages** → the footer follows the content in the normal flow rather than being pinned to the viewport bottom, so it never overlaps content on any page length.
- **Legal text becomes stale as the year moves on** → the year lives in `LEGAL_OWNER` and in the documents' last-updated dates; changing them is a one-line edit and a test failure if the two disagree.
- **The documents are not lawyer-reviewed** → stated plainly in the disclaimer itself, and called out here as a known limitation of the change rather than left implicit.

## Migration Plan

No data migration. The change is additive:

1. Add the `features/legal/` feature area and the `LEGAL_OWNER` constant.
2. Add the five public routes ahead of the `**` catch-all.
3. Add the footer to the sign-in screen and to the shell content area.
4. Add the metadata to the document head and apply it from `LEGAL_OWNER` at runtime.

Rollback is the removal of those four steps. Nothing already in the database, in the API or in the signed-in screens depends on the new files, so reverting leaves the application exactly as it behaves today.

## Open Questions

- **Which city to name for the courts.** The Terms of Use name the laws of India and the courts at the owner's place of residence, because the owner's city is not recorded anywhere in the project. Naming a specific city is a one-line edit to the Terms of Use content and does not affect the approach, the specs or the task breakdown.
- **Whether to also publish numbered screen documents** under `Requirements/screens wise feature/` for the five legal screens, following the existing `01`–`20` convention. Deliberately excluded from this change.
