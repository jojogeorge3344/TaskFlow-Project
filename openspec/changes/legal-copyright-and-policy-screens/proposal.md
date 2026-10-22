# Proposal

## Why

TaskFlow ships with no legal or copyright surface at all: a search of the repository for "copyright", "©" and "all rights reserved" returns nothing. The application holds employee personal data, internal mail with file attachments, activity history and encrypted AI provider keys, and it can relay user content to external processors — Google Gemini, OpenAI, and Google's browser speech service — whenever an administrator selects a cloud provider or the user speaks to TFA. Nothing in the product tells a visitor who owns the software, what they may do with it, what happens to their data, or what is disclaimed. This change adds that surface, and grounds every claim it makes in what the code actually does.

## What Changes

- **Five public legal screens**, reachable without signing in, at `/legal/copyright`, `/legal/terms`, `/legal/privacy`, `/legal/disclaimer` and `/legal/licenses`: the Copyright Notice, the Terms of Use, the Privacy Policy, the Disclaimer, and the Third-Party Licence Notices.
- **A copyright footer** rendered on the sign-in screen and at the end of every authenticated screen's content area, carrying the ownership line and links into the five documents.
- **A shared legal-page renderer** driven by one typed content registry, so all five documents share one layout, one generated table of contents, and one set of accessibility and print rules instead of five hand-maintained templates.
- **Ownership stated as** `© 2026 Jojo George. All Rights Reserved.` with `jojogeorge3344@gmail.com` as the contact address, under the laws of India.
- **Document-level metadata** in `index.html`: page title, author, description and a copyright statement.
- **A `THIRD-PARTY-NOTICES.md`** at the repository root carrying the full licence texts the on-screen notices summarise.
- Every factual claim is written against the implementation as it stands today — PBKDF2 password hashing, local-only storage under the TaskFlow data folder, administrator exclusion from private mail, the cloud-AI and browser-speech exceptions — so the published notices state what the system genuinely does rather than a generic boilerplate position.

## Capabilities

### New Capabilities

- `legal-pages`: The application's public legal and copyright notice surface — the copyright notice, terms of use, privacy policy, disclaimer and third-party licence documents; the shared presentation that renders them; the copyright footer that links to them from the sign-in and authenticated screens; and the document metadata published with the application.

### Modified Capabilities

<!-- None. The shell gains a footer as a new legal surface owned by `legal-pages`; the
     shell's existing requirements about navigation, the header and the account area
     are unchanged, and no archived capability in `openspec/specs/` changes. -->

## Impact

- **Frontend — new feature area**: `TaskFlow Proj/frontend/src/app/features/legal/` holding the content model, the document registry, the shared page component, the footer component and their specs.
- **Frontend — routing**: five top-level public routes in `app.routes.ts`, registered as siblings of `login` outside the `authGuard` shell and ahead of the `**` catch-all, so they resolve without a session.
- **Frontend — existing screens**: `login.component.html` gains the footer below the sign-in card; `shell.component.html` gains the footer at the end of the content area so it scrolls with the page.
- **Frontend — document head**: `index.html` gains the title, author, description and copyright metadata.
- **Repository root**: a new `THIRD-PARTY-NOTICES.md`.
- **Backend and database**: none. No API endpoint, no EF Core migration, no change to authentication or to the permission model — every document is static content served by the frontend.
- **Dependencies**: none added. The pages reuse the existing Angular Router, the existing `styles.css` design tokens and the existing inline SVG icon set.
- **Tests**: frontend specs for the shared page component, the footer component, and the routing behaviour that keeps the documents reachable without a session.
- **Out of scope**: numbered screen documents under `Requirements/screens wise feature/`, a terms-acceptance workflow that gates sign-in, and any server-rendered or externally hosted legal copy.
