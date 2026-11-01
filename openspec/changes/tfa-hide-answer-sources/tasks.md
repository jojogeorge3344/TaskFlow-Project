# Tasks

## 1. Hide sources in the TFA panel

- [x] 1.1 Remove the `@if (sources().length > 0)` Sources block from `frontend/src/app/modules/assistant/components/tfa-message.component.html`, and the `sources` computed, `SourceChip` and `HEADING_SEPARATOR` from `tfa-message.component.ts` (keep `IconComponent` only if the template still uses it); verify `npm run build` passes with no unused-import warnings for the component
- [x] 1.2 Remove the `.tfa-sources`, `.tfa-sources__label`, `.tfa-source` and `.tfa-source__text` rules from `tfa-message.component.css`; verify a search for `tfa-source` under `modules/assistant` finds nothing
- [x] 1.3 Replace the "TfaMessageComponent sources" tests in `tfa-message.component.spec.ts` with one that renders an answer carrying two sources and asserts no `[data-sources]` / `[data-source]` element and no "Sources" text; verify `ng test` for the assistant module passes
- [x] 1.4 Leave `AssistantMessage.sources` in `assistant.model.ts` and the backend unchanged (spec: sources still recorded); verify the backend test suite needs no change and passes

## 2. Docs and manual check

- [x] 2.1 Update the TFA chat workflow (`TF - Documents/TFWorkflow/App WF in MD/09-Assistant/`) and `TF - TDD/TDD-TaskFlow.md` §16.8 wherever they say sources are shown under answers, so they say sources are recorded but not displayed; verify a search of those files for "under the answer" finds no stale wording
- [x] 2.3 Update `TF - BRD/08-TFA-Assistant/08.1-Assistant/BRD-Assistant.md`, `TF - FRD/08-TFA-Assistant/08.1-Assistant/FRD-Assistant.md` and `TF - HLD/08-TFA-Assistant/08.1-Assistant/HLD-Assistant.md` wherever they say sources are shown under answers, so they say sources are recorded but not displayed; verify a search of those three files for "under the answer" and "shows the answer with its sources" finds nothing
- [ ] 2.2 Start the app, ask TFA "what validations apply when creating a task?" as Admin and as Employee, then reload the page; verify the answer appears with no Sources row in both roles and after the reload
