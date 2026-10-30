# Spec Delta

## Purpose

Defines the folder layout and naming rules for the TaskFlow Angular frontend. The UI's code is grouped by the same module hierarchy as the backend and the Role Configuration tree, so any screen's code can be found in one predictable place.

## ADDED Requirements

### Requirement: Module folders mirror the backend hierarchy
Feature code SHALL live under `src/app/modules/` in folders following the backend module → sub-module (→ section) hierarchy, written in kebab-case. The only frontend-only modules SHALL be the public `auth` and `legal` modules.

#### Scenario: Matching paths
- **WHEN** the backend has a sub-module at `Management/EmployeeManagement`
- **THEN** the frontend code for that screen lives under `modules/management/employee-management/`

#### Scenario: Section with its own backend service
- **WHEN** a section such as task discussion has its own backend service and its own frontend component
- **THEN** that component lives under `modules/monitoring/task-management/discussion/`

#### Scenario: No empty folders
- **WHEN** the structure check runs
- **THEN** it reports no empty folder under `src/app`

### Requirement: Layer folders inside each module
Inside a module or sub-module folder, routed screens SHALL be in `pages/`, non-routed components in `components/`, API-calling services in `services/`, module-specific types in `models/` and pure helper functions in `utils/`. No other kinds of file SHALL sit directly in a module folder, except that module's `<module>.routes.ts`.

#### Scenario: Routed screen placement
- **WHEN** a component is referenced by a route definition
- **THEN** its file is inside a `pages/` folder

#### Scenario: API service placement
- **WHEN** a service injects `HttpClient`
- **THEN** its file is inside a module's `services/` folder or in `shared/auth/`

### Requirement: Shared code only when shared
UI components, directives, utilities and non-API services used by two or more modules SHALL live in `src/app/shared/` under `components/`, `directives/`, `services/`, `utils/` or `auth/`. Code used by one module SHALL live in that module. An API service SHALL always live in the module whose backend endpoints it calls, even when other modules import it.

#### Scenario: API service used by several modules
- **WHEN** the employee API service is used by employee management, department members and task assignment screens
- **THEN** it stays in `modules/management/employee-management/services/` and the other modules import it from there

#### Scenario: Single-module helper
- **WHEN** a helper is imported only by the mail module
- **THEN** it lives under `modules/communication/mail/`, not in `shared/`

### Requirement: App-level folders
The app shell and its navigation SHALL be in `src/app/layouts/`, the root route table in `src/app/routes/`, application configuration (providers, API base URL, interceptor registration) in `src/app/config/`, and static assets in `src/assets/`. The legacy `core/` and `features/` folders SHALL NOT exist.

#### Scenario: Legacy folders gone
- **WHEN** the structure check runs after the restructure
- **THEN** neither `src/app/core/` nor `src/app/features/` exists

### Requirement: Consistent file naming
Files SHALL use Angular kebab-case names with a type suffix: `*.component.ts`, `*.service.ts`, `*.model.ts`, `*.routes.ts`, `*.guard.ts`, `*.interceptor.ts`, `*.directive.ts`.

#### Scenario: Suffix check
- **WHEN** the structure check inspects files under `components/`, `pages/` and `services/`
- **THEN** every `.ts` file other than specs has the suffix that matches its folder

### Requirement: Structure is checked automatically
The project SHALL provide `npm run check:structure`, which exits non-zero and lists each offending path when any rule in this capability is broken.

#### Scenario: Misplaced file is caught
- **WHEN** a developer adds `src/app/modules/reports/report-helper.service.ts` outside a `services/` folder
- **THEN** `npm run check:structure` fails and names that file

### Requirement: Behaviour unchanged
The restructure SHALL NOT change any URL, route guard, route data, rendered screen or API request.

#### Scenario: Route table unchanged
- **WHEN** the route specs run after the restructure
- **THEN** every path resolves to the same component with the same guards and `data` as before

#### Scenario: Test suite passes
- **WHEN** `ng test` and `ng build` run after the restructure
- **THEN** both succeed with the same number of passing specs as before
