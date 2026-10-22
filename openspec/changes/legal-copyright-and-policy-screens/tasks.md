# Tasks

## 1. Ownership constants and the content model

- [x] 1.1 Create `TaskFlow Proj/frontend/src/app/features/legal/legal-owner.ts` exporting a `LEGAL_OWNER` constant (owner name, copyright year, contact email, application name, application title, rights statement) and `features/legal/legal-document.model.ts` exporting the `LegalDocument`, `LegalSection` and `LegalBlock` types; verify `npm run build` in `TaskFlow Proj/frontend` compiles and the constant holds "Jojo George", 2026 and `jojogeorge3344@gmail.com`.
- [x] 1.2 Add `features/legal/legal-documents.spec.ts` asserting that every document in the registry carries a title, a summary, a last-updated date and at least one section, and that every section carries a heading and at least one block; verify the spec fails when a document is stripped of a section and passes against the real registry.

## 2. The five legal documents

- [x] 2.1 Write the Copyright Notice into the registry, covering the owner and rights statement, the work the notice protects, the work and names it does not cover, the permitted activities and the prohibited activities, how to credit the work and how to request permission; verify a spec asserts the document's title, its ownership section and both the permitted-use and prohibited-use sections exist.
- [x] 2.2 Write the Terms of Use into the registry, covering acceptance, the licence granted, account responsibilities, acceptable use, the licence TaskFlow needs over user content, intellectual property, third-party AI providers, browser speech recognition, suspension and termination, the disclaimer of warranties, the limitation of liability, indemnity, changes to the terms and the governing law; verify a spec asserts the licence, acceptable-use, liability-limit and governing-law sections exist and that the governing law names India.
- [x] 2.3 Write the Privacy Policy into the registry, covering who the owner is, the categories of data processed and the purpose of each, the storage location, the external AI provider exception, the browser speech recognition exception, who can see what including the exclusion of administrators from private mail, the security measures, the position on sale, analytics and advertising tracking, retention and deletion, the rights of the individual, the position on children and how to make contact; verify a spec asserts the data-categories, storage, AI-provider, browser-speech, mail-privacy and security sections exist.
- [x] 2.4 Re-verify every factual claim in the Privacy Policy against the code — the password hasher registration in `Program.cs`, the storage path in the README and `appsettings.json`, the mail permission checks, the AI provider behaviour and the browser speech behaviour — and correct any claim that does not match the implementation; verify each claim carries a comment naming the file or feature it was checked against.
- [x] 2.5 Write the Disclaimer into the registry, covering the "as is" position and the absence of warranty, the absence of professional or legal advice, the reliability of assistant output and the need to verify it, the advisory nature of prioritisation and allocation suggestions, availability, external links, the pre-release status of the software and the limitation of liability; verify a spec asserts the no-warranty, assistant-output and advisory-suggestion sections exist.
- [x] 2.6 Write the Licence notices into the registry, covering TaskFlow's own All Rights Reserved licence with no permission to reuse, the trademarks in the TaskFlow name, the TFA name and the TF mark, and a third-party table naming each component with its version and its licence; verify a spec asserts the table names the application framework, the runtime, the data-access library, the validation library, the reactive library, the local model runtime and the icon set the interface icons are drawn from, and that the document points at `THIRD-PARTY-NOTICES.md`.
- [x] 2.7 Settle which city the Terms of Use names for the courts, defaulting to the owner's place of residence when no city is specified, and write the decided wording into the governing-law section; verify the governing-law section reads correctly for the settled value.
- [x] 2.8 Add a spec asserting that the owner name, the year and the contact address in every document are read from `LEGAL_OWNER` rather than written out, so that the published documents cannot state a different owner from the footer; verify the spec fails when a document hard-codes a different owner name.

## 3. The shared legal page renderer

- [x] 3.1 Create `features/legal/legal-page.component.ts` resolving the document id from the route's `data` and looking the document up in the registry, with a fallback state for an id that is not in the registry; verify a spec asserts each of the five ids resolves to its own document and that an unknown id renders the fallback rather than an empty page.
- [x] 3.2 Build the page template with the minimal chrome (the TF mark, the application name, the document title, a link back into the application), the table of contents generated from the document's own sections, and the body rendering sections and blocks through Angular control flow with no `innerHTML`; verify a spec asserts the table of contents holds exactly one entry per section in the same order, that each entry links to the section it names, and that the template contains no `innerHTML` binding.
- [x] 3.3 Style the page for reading — a comfortable measure, generous line height and the existing `styles.css` tokens — and confirm the body reflows without horizontal scrolling at a 360-pixel viewport; verify a spec asserts the narrow-viewport reflow rule is present in the component stylesheet.
- [x] 3.4 Add print rules scoped to the legal page inside `@media print`, keeping the text and headings legible and dropping the screen-only furniture; verify a spec asserts the print rules are scoped to the component and that they do not leak into other screens' styles.
- [x] 3.5 Implement the link back into the application so a visitor is taken to the sign-in screen while a signed-in reader is taken to the dashboard for their role and is not signed out, using the `AuthService` session signal and the existing `dashboardRouteFor` helper; verify a spec asserts the link's target and label differ between a visitor and a signed-in reader.

## 4. Public routing

- [x] 4.1 Register the five legal routes in `app.routes.ts` as siblings of `login`, outside the `authGuard` shell route and ahead of the `**` catch-all, each carrying its document id in the route's `data`; verify `app.routes.spec.ts` asserts that each of the five routes resolves to the legal page component.
- [x] 4.2 Add a routing spec proving that each of the five legal addresses renders its document with no session present and is not redirected to `/login`; verify the spec passes with no authenticated user in the test bed.
- [x] 4.3 Confirm that the `**` catch-all still sends an unknown address to `/login` and that an unknown address under `/legal/` behaves consistently with it; verify the routing spec covers both cases.

## 5. The copyright footer

- [x] 5.1 Create `features/legal/legal-footer.component.ts` with a `variant` input of `surface` and `dark`, rendering the ownership line and a link to each of the five legal documents, with the ownership line built from `LEGAL_OWNER`; verify a spec asserts that both variants render the ownership line and all five links with the same wording.
- [x] 5.2 Mark the document currently being read with `aria-current="page"` on its footer link; verify a spec asserts the active document's link carries `aria-current="page"` and that the other four do not.
- [x] 5.3 Place the footer inside `.content__inner` after the router outlet in `shell.component.html`, so it inherits the reading column and sits in the normal flow; verify `shell.component.spec.ts` still passes and a new assertion confirms the footer is rendered in the shell.
- [x] 5.4 Place the footer below the sign-in card in `login.component.html` and change `.login` to a centred column so the card and the footer stack rather than sitting side by side; verify `login.component.spec.ts` still passes, a new assertion confirms the footer is rendered, and the sign-in card remains centred at a 1280 by 800 viewport.

## 6. Published page metadata

- [x] 6.1 Add the page title, the author, the description and the copyright statement to `index.html`, with the static values matching `LEGAL_OWNER` so the head is correct before Angular boots; verify a spec parses `index.html` and asserts each of the four values matches the constant.
- [x] 6.2 Apply the same title, author, description and copyright statement from `LEGAL_OWNER` at runtime on every page using the Angular `Title` and `Meta` services; verify a spec asserts the document title and the author meta are set from the constant after bootstrap.

## 7. Third-party notices file

- [x] 7.1 Create `THIRD-PARTY-NOTICES.md` at the repository root naming each third-party component with its version and its licence and carrying the full licence texts the on-screen notices summarise; verify the file names every component the Licence notices screen lists and states the licence of each.

## 8. Integration checks

- [ ] 8.1 Run the full frontend test suite and a production build; verify `npm test` and `npm run build` in `TaskFlow Proj/frontend` both succeed with no failing specs.
- [ ] 8.2 Walk each of the five legal addresses in a browser with no session and confirm the document renders, the table of contents navigates to its sections, the narrow-viewport layout reflows cleanly at 360 pixels, the print preview is legible, and the footer's links reach all five documents; verify the walkthrough is recorded against this change.

## Workflow follow-up

- Archive the change once the review requirements for the project are satisfied, so the `legal-pages` capability is written into `openspec/specs/`.
- Verify the archived result places the `legal-pages` spec under `openspec/specs/legal-pages/spec.md` with its requirements intact.
