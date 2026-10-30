# Proposal

## Why

REQ-05 requires the frontend to use the same module and child-module hierarchy as the backend, with `components/`, `pages/` and `services/` per module and dedicated `shared/`, `layouts/`, `routes/`, `assets/` and `config/` folders. Today the Angular app splits each module's code across three places: `features/<x>/` for components, a flat `core/services/` (18 API services) and a flat `core/models/` (14 model files). Folder names (`employees`, `tasks`, `settings`) don't match the module tree in Role Configuration, and the app shell and assistant panel sit in `core/layout/`.

## What Changes

- Introduce `src/app/modules/` organised by the same module → sub-module (→ section) hierarchy as the backend change `backend-clean-architecture` (from `PermissionCatalog`). Each leaf gets `pages/` (routed screens), `components/` (non-routed pieces) and `services/` (API calls), plus `models/` where the module has its own types.
- Move every `core/services/*.service.ts` and `core/models/*.model.ts` into the module that owns it. Delete the `core/models/index.ts` barrel.
- Add the frontend-only public modules `auth` (login, account recovery, security setup) and `legal`. They have no permission-catalog entry because they run before sign-in.
- Restructure shared code into `shared/components/`, `shared/directives/`, `shared/services/`, `shared/utils/` and `shared/auth/` (guards, interceptors, token store, permission keys). Shared code is only what more than one module uses.
- Move the app shell, header, nav items and theme menu to `layouts/`. Move `app.routes.ts` to `routes/`, with one `<module>.routes.ts` per module loaded through `loadChildren`. Move `app.config.ts` and the API base-URL setup to `config/`. Move `public/` to `src/assets/`.
- Use kebab-case folder names matching the backend's PascalCase module names (`Management/EmployeeManagement` → `management/employee-management`).
- Add a structure check (`npm run check:structure`) that fails when a file sits outside the allowed layout.
- **No behaviour change:** every URL, guard, permission check, screen and API call stays as it is.

## Capabilities

### New Capabilities
- `frontend-architecture`: the module-based folder layout, the layer folders inside each module, shared/layout/route/config placement, and naming rules the Angular app must follow (REQ-05 frontend), checked by an automated structure script.

### Modified Capabilities
<!-- None: no main specs exist yet. -->

## Impact

- **Code:** about 148 source files and 95 spec files under `frontend/src/app` move, and their relative imports are rewritten. No component, template or service logic changes.
- **Routing:** `app.routes.ts` is split into per-module route files. URLs, guards and route `data` are unchanged, which `app.routes.spec.ts` and `app.routes.recovery.spec.ts` verify.
- **Build config:** `angular.json` asset input changes from `public` to `src/assets` (two places). A `check:structure` npm script is added.
- **Independence:** this change is independent of `backend-clean-architecture` and can be applied before, after or alongside it. Its only touch point is `login.component.ts`, which the backend change edits for error parsing. Whichever change lands second rebases that one line.
- **Dependencies:** none added.
