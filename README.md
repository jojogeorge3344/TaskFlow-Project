# TaskFlow – Intelligent Task Allocation & Tracking System

> Personal Project · Skill Development (2026)
> **Author:** Jojo George · jojogeorge3344@gmail.com
> © 2026 Jojo George. All rights reserved.

TaskFlow is a web application that helps an organization **allocate work to employees, track progress and never miss a
deadline**. Managers create and assign tasks, employees update and discuss them, and a background reminder engine notifies
the assignee and selected recipients as deadlines approach and pass. Administrators manage people, departments, projects,
roles and settings. Everyone gets dashboards, workload views, activity history and reports scoped to their role. The
built-in AI assistant, **TFA**, answers questions, suggests priorities and drafts mail, and it can also be used by voice.

---

## Contents

1. [Features](#features)
2. [Technologies](#technologies)
3. [Architecture](#architecture)
4. [Repository layout](#repository-layout)
5. [Getting started](#getting-started)
6. [Running the tests](#running-the-tests)
7. [Documentation](#documentation)
8. [Data folder and first sign-in](#data-folder-and-first-sign-in)
9. [Roles and permissions](#roles-and-permissions)
10. [Mail](#mail)
11. [Two-step verification (authenticator app)](#two-step-verification-authenticator-app)
12. [TFA, the AI assistant](#tfa-the-ai-assistant)
13. [Backend structure](#backend-structure)
14. [Frontend structure](#frontend-structure)
15. [Copyright](#copyright)

---

## Features

| Module | What it covers |
|---|---|
| **Authentication** | Sign-in with email or username, mandatory two-step verification (authenticator app and backup codes), password recovery with an authenticator code, JWT access and refresh-token sessions |
| **Dashboard** | Role-specific dashboards for Administrator, Manager and Employee, with summary cards, charts, filters, and drill-down into the matching tasks |
| **Management** | Employee management (accounts, roles, activation), department management and project management (teams, positions, allocations) |
| **Monitoring** | Task management (create, assign, update status, reassign, recipients), comments and activity history, Kanban board, employee workload |
| **Communication** | In-app notification center, automatic deadline reminders, and internal mail (folders, drafts, attachments, search) |
| **Reports** | Task summary report with filters and CSV export |
| **Profile** | My profile, change password, self-service two-step verification |
| **Settings** | Application settings, reminder settings, role configuration (custom roles from a fixed permission catalog) and TFA (AI provider) settings |
| **TFA Assistant** | AI chat assistant that follows your role's permissions, with confirm-before-acting actions, help topics, an audit log, rate limits and hands-free voice |
| **Legal** | Copyright notice, terms of use, privacy policy, disclaimer and licences, plus an ownership footer on every screen |

Roles: **Administrator**, **Manager**, **Employee** (system roles), plus **custom roles** built from the permission catalog.

---

## Technologies

### Application stack

| Layer | Technology | Version |
|---|---|---|
| Backend runtime | .NET (target framework `net10.0`) | 10 |
| Web API | ASP.NET Core Web API | 10 |
| Data access | Entity Framework Core (+ SQLite provider, Design tools) | 10.0.12 |
| Database | SQLite (`taskflow.db`) | 3.x (via EF Core provider) |
| Validation | FluentValidation (+ DependencyInjectionExtensions) | 12.2.0 |
| Authentication | Microsoft.AspNetCore.Authentication.JwtBearer · System.IdentityModel.Tokens.Jwt | 10.0.12 · 8.23.0 |
| API description | Microsoft.AspNetCore.OpenApi | 10.0.8 |
| Dev integration | Microsoft.AspNetCore.SpaProxy (starting the API also launches the Angular dev server) | 10.0.12 |
| Background jobs | `BackgroundService` reminder engine (`ReminderSchedulerService`) | built-in |
| Password hashing / MFA | ASP.NET Core `PasswordHasher<T>` (PBKDF2) · TOTP authenticator codes · DPAPI key ring | built-in |
| Frontend framework | Angular (standalone components) + Angular CLI | 19.2 |
| Language | TypeScript | 5.7 |
| Reactive library | RxJS | 7.8 |
| Charts | Chart.js · ng2-charts | 4.6 · 8.0 |
| Drag and drop | Angular CDK | 19.2 |
| QR codes (MFA setup) | qrcode | 1.5 |
| Styling | Custom CSS design tokens and `.tf-*` primitives (no CSS framework) | – |
| Backend tests | xUnit (`TaskFlow.UnitTests` – including the `Architecture/` rules – and `TaskFlow.IntegrationTests`) | – |
| Frontend tests | Jasmine 5.6 · Karma 6.4 · folder-structure check (`npm run check:structure`) | – |
| Runtime tools | Node.js (22 LTS recommended) · .NET 10 SDK | – |

### AI inside the application (TFA assistant)

| Provider | Model / default | Notes |
|---|---|---|
| Ollama (local, free) | `llama3.2:3b` at `http://localhost:11434` | Installed by `scripts/setup-ai.ps1` / `setup-ai.sh`; on by default for new installations |
| Google Gemini | Built-in model list in `AiProviderCatalog.cs` | Cloud; free tier with rate limits; API key entered in TFA Settings |
| OpenAI GPT | Built-in model list in `AiProviderCatalog.cs` | Cloud; paid API key |
| Ollama embeddings (local, free) | `nomic-embed-text` (768 dimensions) | Embeds the `TF - Documents` Markdown files so TFA can answer from them; always local, whichever chat model is used; installed by the same setup scripts |

### AI-assisted and spec-driven development

| Tool | Version | Used for |
|---|---|---|
| **Claude** (Anthropic) | Claude Opus 5.5 (documentation and TDD sync) | Requirements analysis, design, code generation and review, documentation |
| **Claude Code** | 2.1.292 | AI coding agent in the repository; runs the project skills and `/opsx` commands |
| **OpenCode** (with `@opencode-ai/plugin`) | plugin 1.19.0 | Second AI coding agent; reads the same `.claude/skills` |
| **OpenSpec** (spec-driven development CLI) | 1.14.1 | Every feature is planned as a change with proposal → design → specs → tasks before it is built (`openspec/changes/`, 38 changes) |
| Git · GitHub · GitHub CLI (`gh`) | – | Version control, branches, pull requests and PR reviews |

### Claude Code skills, commands and plugins

| Name | Type | Purpose |
|---|---|---|
| `openspec-propose`, `openspec-explore`, `openspec-apply-change`, `openspec-update-change`, `openspec-sync-specs`, `openspec-archive-change` | OpenSpec skills (`/opsx:propose`, `/opsx:explore`, `/opsx:apply`, `/opsx:update`, `/opsx:sync`, `/opsx:archive`) | Spec-driven workflow: propose, explore, implement, update, sync and archive changes |
| `requirements-to-markdown` | Custom skill | Converts BRD/requirement DOCX or PDF into faithful Markdown |
| `brd-writer` | Custom skill | Writes a Business Requirements Document (overall or per sub-module) from a fixed template |
| `frd-writer` | Custom skill | Writes a Functional Requirements Document with use cases, validations, permissions and traceability |
| `hld-writer` | Custom skill | Writes a High-Level Design: context, components, data, flows, security, deployment, decisions |
| `lld-writer` | Custom skill | Writes a Low-Level Design: classes, endpoints, DTOs, tables, sequence diagrams, FR → code → test |
| `module-docs` | Custom skill | Generates the module-wise Application and Technical workflow documents (MD + PDF) |
| `tdd-sync` | Custom skill | Keeps the TDD in sync with the code (fact extraction + verification scripts) |
| `commit-pr-review` | Custom skill | Branch, commit, push, open a PR and post a consolidated code review |
| `manual-code-review` | Custom skill | Reviews a GitHub pull request and posts one findings comment |
| `code-review-resolved` | Custom skill | Verifies fixes on a reviewed PR and posts a "Resolved Items" comment |
| `@opencode-ai/plugin` | OpenCode plugin | Lets OpenCode use the shared skills and commands |
| `scripts/openspec-update.ps1` | Helper script | Runs `openspec update` without duplicating skills |

### Documentation set

All project documents are in **`TF - Documents/`**. The BRD, FRD, HLD and LLD sets share one folder layout: `00-Overall`, then one
folder per module with one sub-folder per sub-module (16 in total), each holding the Markdown file and its PDF.

| Document | What it covers | Location | Formats | Status |
|---|---|---|---|---|
| **BRD** – Business Requirements Document (overall + 16 sub-modules) | Business goals, roles, scope, workflows, screen requirements, business rules, NFRs, acceptance criteria | `TF - Documents/TF - BRD/` | Markdown, PDF | **Approved v1.0** – signed off 31-Oct-2026 |
| **FRD** – Functional Requirements Document (overall + 16) | Functional requirements, use cases, validations, messages, permissions, traceability to the BRD | `TF - Documents/TF - FRD/` | Markdown, PDF | Available (v1.0, draft) |
| **HLD** – High-Level Design (overall + 16) | Context, architecture, components, data, key flows, security, deployment, design decisions | `TF - Documents/TF - HLD/` | Markdown, PDF | Available (v1.0, draft) |
| **LLD** – Low-Level Design (overall + 16) | Classes and files, every API endpoint, DTOs, validators, database tables, sequence diagrams, FR → code → test | `TF - Documents/TF - LLD/` | Markdown, PDF | Available (v1.0, draft) |
| **TDD** – Technical Design Document | Whole-system technical design synced to the code; reference documents and traceability map (§1.5, §1.6) | `TF - Documents/TF - TDD/TDD-TaskFlow.md` / `.pdf` | Markdown, PDF | Available (v2.1) |
| **Development Plan** | Screen-by-screen feature plan (20 screens) | `TF - Documents/Screens Wise Feature Development Plan/` | Markdown | Available |
| **Permission documents** | All permissions with purpose; Administrator, Manager and Employee permissions module-, sub-module- and screen-wise | `TF - Documents/Permission Requirement/Permissions in Excel/` and `Permissions in PDF/` | Excel, PDF | Available |
| **Workflow docs** | Application and technical workflow per module and child module (10 modules) | `TaskFlow Proj/TFWorkflow/` and `TaskFlow Proj/TFTechnicalWorkflow/` | Markdown, PDF | Available |
| **Specs** | Spec-driven change records: proposal, design, delta specs and tasks per feature | `openspec/changes/` (38 changes) | Markdown, HTML mockups | Available |
| Original BRD | First requirement statement (20 screens); superseded by the approved BRD set | Retired – no longer in the repository; its content lives on in the BRD set and the Development Plan | DOCX | Superseded |
| README / project.md / agent.md | Setup, operations, structure rules, contributor rules | repository root and `TaskFlow Proj/` | Markdown | Available |

### Documentation formats and tooling

| Format | Used for |
|---|---|
| **Markdown (`.md`)** | Source of truth for the BRD, FRD, HLD, LLD, TDD, workflow documents, specs and READMEs; diagrams written in Mermaid |
| **DOCX (`.docx`)** | Original BRD (v1.0, superseded by the Markdown BRD set and retired from the repository) |
| **PDF (`.pdf`)** | Print/share copy next to every Markdown document (BRD, FRD, HLD, LLD, TDD, workflow docs, permission documents), with rendered diagrams and the copyright footer |
| **Excel (`.xlsx`)** | Permission documents (all permissions; Administrator, Manager, Employee) |
| HTML | UI mockups inside OpenSpec changes |

PDFs are generated from the Markdown with Mermaid CLI (diagrams), Pandoc (HTML) and Chromium (print to PDF).

---

## Architecture

```
┌──────────────────────────┐   HTTPS / JSON   ┌──────────────────────────────────┐
│  Angular SPA (browser)   │ ◄──────────────► │  ASP.NET Core Web API (.NET 10)  │
│  modules, guards,        │   Bearer JWT     │  Controllers  (HTTP only)        │
│  services, Chart.js      │                  │  Services     (business rules)   │
└──────────────────────────┘                  │  Repositories (data access)      │
                                              │  Integrations (AI, JWT, storage) │
                                              │  Reminder engine (background)    │
                                              └───────────────┬──────────────────┘
                                                              │ EF Core
                                                              ▼
                                                  ┌───────────────────────┐
                                                  │ SQLite  (taskflow.db) │
                                                  └───────────────────────┘
```

The backend follows **Clean Architecture with the Repository pattern**: a request flows
**Controller → Service → Repository → database**, and each layer calls only the one below it, through its interface.
The rules are enforced by automated architecture tests (see [Backend structure](#backend-structure)).

---

## Repository layout

```
TaskFlow Project/
├── README.md                 this file
├── TF - Documents/
│   ├── TF - BRD/             approved BRD set (00-Overall + 16 sub-modules, MD + PDF)
│   ├── TF - FRD/             FRD set, same layout
│   ├── TF - HLD/             HLD set, same layout
│   ├── TF - LLD/             LLD set, same layout
│   ├── TF - TDD/             TDD-TaskFlow.md / .pdf
│   ├── Screens Wise Feature Development Plan/   the 20 screen-wise requirement files
│   └── Permission Requirement/                  permission documents (Excel and PDF)
├── .claude/skills/           Claude Code skills (OpenSpec, documentation writers, tdd-sync, code review)
├── openspec/                 OpenSpec planning workspace (changes and specs)
├── scripts/                  setup-ai.ps1 / setup-ai.sh (local AI setup), openspec-update.ps1
└── TaskFlow Proj/
    ├── backend/              TaskFlow.sln, src/TaskFlow.Api, tests/ (UnitTests, IntegrationTests)
    ├── frontend/             Angular 19 workspace (src/app/modules/…)
    ├── TFWorkflow/           functional (application) workflow docs, in MD and PDF
    ├── TFTechnicalWorkflow/  technical workflow docs, in MD and PDF
    ├── project.md            project overview for developers
    └── agent.md              rules for AI and automation contributors
```

---

## Getting started

### Prerequisites

- **.NET 10 SDK** (`dotnet --list-sdks` shows 10.0.x)
- **Node.js 22 LTS** (recommended; Angular 19 supports `^18.19.1 || ^20.11.1 || ^22.0.0`)
- Optional: **Visual Studio 2022 17.8+ / 2026** with the *ASP.NET and web development* workload, for F5 debugging
- Optional: **Ollama**, for the free local AI assistant (see [Free local AI in one command](#free-local-ai-in-one-command-ollama))

### Run the application

```powershell
# 1. Install frontend packages (first time only)
cd "TaskFlow Proj/frontend"
npm install

# 2. Start the API. It also starts the Angular dev server.
cd ../backend
dotnet run --project src/TaskFlow.Api
```

Or open `TaskFlow Proj/backend/TaskFlow.sln` in Visual Studio and press **F5**.

| What | Address |
|---|---|
| Web application | `http://localhost:4200` |
| API | `https://localhost:7055` (the SPA calls it through `/api/*`) |

Trust the development certificate once with `dotnet dev-certs https --trust`. On the first start the API creates the
data folder and the database and applies all migrations. In Development it also creates the three demo accounts listed under
[Data folder and first sign-in](#data-folder-and-first-sign-in).

### Optional: free local AI for TFA

```powershell
.\scripts\setup-ai.ps1        # Windows
./scripts/setup-ai.sh         # macOS / Linux
```

---

## Running the tests

| Tests | Command | Folder |
|---|---|---|
| Backend unit and integration tests (including architecture rules) | `dotnet test` | `TaskFlow Proj/backend` |
| Frontend folder-structure check and unit tests | `npm test` | `TaskFlow Proj/frontend` |
| Frontend folder-structure check only | `npm run check:structure` | `TaskFlow Proj/frontend` |

---

## Documentation

| Document | Location |
|---|---|
| Business Requirements – approved BRD set (overall + 16 sub-modules) | `TF - Documents/TF - BRD/` (start with `00-Overall/BRD-TaskFlow-Overall.md`) |
| Functional Requirements – FRD set | `TF - Documents/TF - FRD/` |
| High-Level Design – HLD set | `TF - Documents/TF - HLD/` |
| Low-Level Design – LLD set | `TF - Documents/TF - LLD/` |
| Technical Design Document | `TF - Documents/TF - TDD/TDD-TaskFlow.md` |
| Screen-wise requirements (01–20) | `TF - Documents/Screens Wise Feature Development Plan/` |
| Permission documents (all permissions; Administrator, Manager, Employee) | `TF - Documents/Permission Requirement/` |
| **Application workflow, per module and child module** (purpose, screens, who can see and use each feature, permissions, step-by-step flows) | `TaskFlow Proj/TFWorkflow/App WF in MD` and `App WF in PDF` |
| **Technical workflow, per module and child module** (endpoints, services, repositories, database, security, tests, known gaps) | `TaskFlow Proj/TFTechnicalWorkflow/Tech WF in MD` and `Tech WF in PDF` |
| Developer overview / contributor rules | `TaskFlow Proj/project.md`, `TaskFlow Proj/agent.md` |

Each BRD, FRD, HLD and LLD sub-module uses the same folder and file stem, for example
`TF - Documents/TF - BRD/02-TaskFlow-Monitoring/02.3-Board/BRD-Board.md` and `TF - Documents/TF - LLD/02-TaskFlow-Monitoring/02.3-Board/LLD-Board.md`.
TDD §1.6 maps every sub-module to its documents, its original BRD screens and its TDD sections.

The workflow docs have one folder per module (`01-Auth` … `10-Legal`). In each folder, `NN.0-<Module>-Overview` comes
first, followed by one document per child module (for example `03-Management/03.1-Employee-Management`).

---

## Data folder and first sign-in

TaskFlow keeps everything it stores on this machine in one folder, **`C:\TaskFlow`**. The API creates it on startup if it
is missing and stops with a clear message if it cannot create or write to it. Inside:

| Item | What it is |
|---|---|
| `taskflow.db` | The SQLite database |
| `dp-keys\` | The encryption key ring for AI provider API keys |
| `mail-attachments\` | Files attached to internal mail |

On Windows the default is `C:\TaskFlow`; on Linux and macOS it is a `TaskFlow` folder under the shared application data
folder (for example `/usr/share/TaskFlow`). Set `Storage:Root` in `appsettings.json` (or the `Storage__Root` environment
variable) to use another folder. The old
`database/` folder inside the solution is no longer used and is not migrated; delete it when you no longer need it.
Back up the whole `C:\TaskFlow` folder, but keep `dp-keys` out of any backup that also holds the database.

On the first run the API creates the database, applies the schema and adds three accounts when `Seed:Enabled` is on. It is on in
Development (`appsettings.Development.json`) and **off by default everywhere else**, so a deployment never ships with
these well-known passwords; set `Seed:Enabled` to `true` yourself, together with the three passwords below, to create them. An account that already exists is never changed.

| Role | Email | Password | Lands on |
|---|---|---|---|
| Administrator | `admin@taskflow.com` | `123456` | `/admin` |
| Manager | `manager@taskflow.com` | `123456` | `/manager` |
| Employee | `employee@taskflow.com` | `123456` | `/me` |

These are development defaults. Change the passwords after signing in, or set `Seed:AdministratorPassword`,
`Seed:ManagerPassword` and `Seed:EmployeePassword` before the first run, for anything beyond local use.

### Email addresses are on one fixed domain

Every employee email ends in `@taskflow.com`. The screens show that suffix as fixed text and you type only the part before it; the
API rejects any other domain, and two people cannot share the same part before the `@`, even if one of them is deleted.

**Upgrading an existing database is a breaking change for sign-in.** The first start after the upgrade rewrites every existing email
to `<name>@taskflow.com`, keeping the part before the `@` (so `ravi@gmail.com` becomes `ravi@taskflow.com`). Characters the new rules do not allow are
dropped (`o'brien` becomes `obrien`). If two users would end
up with the same name, the one created later gets a number (`jo@gmail.com` and `jo@yahoo.com` become `jo@taskflow.com` and
`jo2@taskflow.com`). Passwords and usernames are unchanged, so users can also sign in with their username. The original domains are
not stored and cannot be restored, so **back up the database file before upgrading**.

## Roles and permissions

Every user holds exactly one role, and a role is a set of permissions chosen from a **fixed catalog** (tasks, reports,
workload, departments, employees, settings and so on). The API checks the permission on every request, so a change to a
role applies to its users on their very next action, without signing in again.

- **System roles** — Administrator, Manager and Employee are created by the application. Their permissions are
  **read-only**: nobody, including an Administrator, can change, rename or delete them, in the UI, the API or the database.
- **Custom roles** — an Administrator can create, edit and delete roles under **Settings → Role Configuration**
  (Administrator only), ticking the permissions each one gets, then give a role to a user from **Employee Management**.
  A role that users still hold cannot be deleted; move those users to another role first.
- **The permission list is fixed.** Administrators choose from it but cannot add or remove permissions, so every
  checkbox is something the application actually enforces. Managing roles is Administrator-only and is never offered
  for a custom role.
- The last active Administrator cannot be moved to another role.
- A custom role lands on the personal dashboard (`/me`) and sees only the menu entries its permissions open.

## Mail

TaskFlow has its own internal mail, like Gmail but only between TaskFlow users. Open **Mail** in the side menu,
available to every role.

- **Folders:** Inbox, Starred, Important, Sent, Drafts and Trash, with unread counts. The Mail menu entry shows how
  many Inbox mails are unread.
- **Compose:** write to one or more colleagues in **To** and **Cc**. Recipients are picked from TaskFlow users by name
  or email. An address that is not an active TaskFlow user (for example a Gmail address) is rejected, and nothing
  ever leaves TaskFlow.
- **Reply, Reply all and Forward** keep mails together as one conversation. Forwarding includes the original
  attachments.
- **Drafts:** save, edit and send later, or discard.
- **Trash:** restore mail, or delete it forever. These actions affect only your own copy.
- **Search:** search your own mailbox by subject, text, sender or recipient.
- **Privacy:** each person sees only their own mail. Administrators have no access to other people's mail.

### Attachments

| Limit | Value |
|---|---|
| Per file | 10 MB |
| Per mail | 5 files, 25 MB in total |
| Blocked types | `.exe .dll .bat .cmd .com .msi .ps1 .sh .js .vbs .scr` |

Files are stored under generated names in the folder set by `Mail:AttachmentsPath` in `appsettings.json`. The
default is `mail-attachments`, resolved under the data folder (`C:\TaskFlow`), so it sits next to `taskflow.db`. Only the uploader and people who have the mail in their mailbox can download a file.
**Back up this folder together with the database.**

### Deadline reminders in Mail

Every automatic deadline reminder also arrives in the recipient's Inbox as a mail from **TaskFlow**, with a link to
the task. These mails cannot be replied to, but they can be forwarded. To turn this off, clear
**Reminder Settings → Also deliver reminders to Mail**. Bell notifications are always sent either way.

`Mail:AppBaseUrl` sets the address used for the task link (default `http://localhost:4200`).

## Two-step verification (authenticator app)

Everyone must connect a free authenticator app (Google Authenticator, Microsoft Authenticator, Authy, 1Password and
similar) before they can use TaskFlow. It costs nothing, and the codes work offline.

**First sign-in.** Anyone who has not set it up is taken to a setup screen instead of the app, and the API refuses every
other request until it is done:

1. **Your details.** Confirm your name and email, see your employee details, and enter your **mobile number** (international
   format, for example `+919876543210`). The number is a contact detail only; nothing is ever sent to it. It does not have
   to be unique: **email is the only unique identity**, so colleagues may share a number. Saving is refused if another
   record already holds the same email and number.
2. **Authenticator app.** Confirm your password, scan the QR code (or type the key into the app) and enter the 6-digit code.
3. **Backup codes.** You are shown **10 backup codes once**. Save them somewhere safe; each works one time if you lose
   your phone. Tick the box and open TaskFlow.

**Signing in** after that takes your password, followed by the current code (or a backup code). A code can only be used
once, and 5 wrong codes in a row lock code entry for 5 minutes.

- **Forgot your password?** On the sign-in page choose **Forgot password?**, enter your username or email and the current
  code, then set and confirm a new password. Backup codes are not accepted here. The page gives the same answer whether or
  not the account exists, and no password is ever sent by email or text message. Attempts here are throttled on their own, reserved before the code is checked so parallel requests cannot beat it
  (5 per account and 20 per address, the address pooled by /64 for IPv6, in 5 minutes) and never lock the owner out of sign-in. The owner gets an in-app
  notification when a password was reset this way. There is no administrator-generated or emailed password.
- **Nobody can turn it off.** Two-step verification is a security layer, so there is no switch for it, not for users and not
  for administrators. If a phone and its backup codes are both lost, see the next bullet.
- **Guessing the current password is throttled.** Re-entering it (change password, set up MFA, new backup codes) allows 5 tries per 5 minutes per account, so an access token alone cannot be used to guess it.
- **Changing your password asks for a code too.** On **Profile → Security → Change password**, enter your current password,
  the new one (twice) and a fresh code from the app (or a backup code). A mistyped current password does not use up the code.
- **Passwords stay hidden.** The **Show** button in that card first asks for a 6-digit code. Once it checks out, the boxes
  can be shown for about five minutes, then lock again. (TaskFlow never stores or returns anyone's real password, only a
  hash, so what is shown is only what you typed.)
- **Lost phone and lost backup codes.** An Administrator (or anyone who can manage employees) opens the employee's
  details and chooses **Reset MFA**. The person is signed out everywhere, notified, and sets the app up again at their
  next sign-in. This is the only way MFA is ever removed.
- **Clock.** Codes depend on the phone's time. If valid codes are refused, switch on automatic date and time on the
  phone. A code is accepted for about 30 seconds either side of the server's time.

| Setting | Meaning |
|---|---|
| `Mfa:Required` | `true` (default): nobody can use the app until two-step verification is set up. Set `Mfa__Required=false` only as a break-glass measure, for example if every Administrator is locked out, and turn it back on afterwards. Turning it on for an existing database sends every user without an authenticator to the setup screen on their next request, so announce the rollout first. |
| `Proxy:KnownProxies` | Empty by default, which trusts only a loopback proxy. When the API runs behind a reverse proxy, list its IP addresses here (`Proxy__KnownProxies__0=10.0.0.5`) so the real client address reaches the password-recovery throttle. Without it every caller behind the proxy shares one address budget. |
| `Mfa:Issuer` | The name the authenticator app shows beside the account (default `TaskFlow`). |

The key is stored encrypted with the same key ring as the AI provider keys (`AiProviders:KeyRingPath`), so keep that
folder with the database in backups, or every user with MFA would need an administrator reset. Backup codes are stored
only as hashes. Nothing secret is logged.

## TFA, the AI assistant

TFA is a chat assistant in a small panel on the right of every signed-in screen. Open it with the **TFA** button, by
typing, or by saying **"Hey TF assistant"**. It answers questions about TaskFlow from the project documents, tells you what is upcoming or overdue,
suggests what to work on first, and drafts mail for you to review. Replies are written, in the same window.

### What TFA can do

| Ask | What happens |
|---|---|
| "What validations apply when creating a task?" | Answered only from the TaskFlow documents (`TF - Documents`), with the document sections it used shown as **Sources** under the answer |
| "How do I change a task's status?" | The same: how-to questions are answered from the BRD, FRD and screen documents |
| "What are my upcoming tasks?" | Your open tasks, from the same data and access rules as the Task List |
| "What should I prioritise?" | The system ranks your tasks (overdue first, then due today, then by deadline and priority). The model only explains the order, so every model gives the same one |
| "Send a mail to my manager about TF-102" | A draft appears with **Send mail / Edit / Cancel**. Nothing is sent until you click **Send mail** |
| "Change my name to …" | Prepared as a change you confirm. Only your own name and email can be changed this way |

Everything TFA does follows your role. It uses the same permission checks as the rest of the application, on the
server, whether you typed or spoke. For example, an Employee who asks to become a Manager is told that only an
authorized administrator can do that, and nothing changes. Only an Administrator can ask TFA to change another user's
role, and that also needs a confirmation. TFA does not change your own role even for an Administrator.

**Passwords never go through chat.** TFA explains the steps and links to **My Profile**. A message that looks like it
contains a password or an API key is not sent to any model and is not stored.

### Answers from the TaskFlow documents

Questions about TaskFlow's features, screens, rules and design are answered **only** from the Markdown files in
`TF - Documents` (the BRD, FRD, HLD and LLD sets, the TDD and the screen specs). When the documents do not cover a
question, TFA says so instead of guessing, and it does not answer general-knowledge questions. Questions about your own
tasks, priorities, mail and profile still use your live data.

- **How it works.** The files are split into sections by heading, embedded with the free local Ollama model
  `nomic-embed-text` and stored in the database (tables `DocumentSources` and `DocumentChunks`). For each message TFA
  finds the most similar sections and gives them to the chat model as reference text. This happens before the model is
  called, so it works with every model, including small local ones that cannot call tools.
- **Sources.** An answer that used the documents shows the sections under it as chips (document and heading; hover for
  the full heading path and file). They are shown again when the conversation is reloaded.
- **Keeping up to date.** The API indexes the folder when it starts and checks it every 5 minutes. Only files whose
  content changed are embedded again, and deleted files are removed, so editing a document updates TFA's answers
  without a restart. The first index of the whole folder takes a while on a laptop CPU (about 30 minutes for 89 files
  and 3,300 sections); TFA answers from what is already indexed meanwhile, and later checks take seconds.
- **Document search card.** **Settings → TFA Settings → Document search** shows the folder, the embedding model, the
  number of files and sections, when indexing last finished and the last error. **Re-index now** (needs the
  `settings.ai.edit` permission) checks the folder immediately.
- **Without Ollama** TFA still answers questions about your own work, and says the documents cannot be searched right
  now. Embeddings always run on Ollama, even when the chat model is Gemini or OpenAI; with a cloud chat model, the
  matching document text is sent to that provider with your message (the Cloud notice says so).
- **Every role** can get answers from every document. PDF and Excel files are not read (the PDFs are copies of the
  Markdown files or of the permission spreadsheets).

Settings, in the `DocumentKnowledge` section of `appsettings.json` (or environment variables such as
`DocumentKnowledge__SourcePath`):

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | `false` switches indexing and document answers off |
| `SourcePath` | `../../../../TF - Documents` | The documents folder. A relative path resolves against the API project folder (`TaskFlow Proj/backend/src/TaskFlow.Api`), so the default is the workspace's `TF - Documents` |
| `EmbeddingModel` | `nomic-embed-text` | Changing it re-embeds every document |
| `OllamaBaseUrl` | empty | The Ollama server for embeddings; empty uses the Ollama address saved in TFA Settings, then `http://localhost:11434` |
| `TopK` / `MinScore` | `4` / `0.6` | At most this many sections, each at least this similar, are given to the model |
| `MaxChunkCharacters` / `MaxContextCharacters` | `1500` / `4000` | Section size before splitting, and the most document text per message |
| `RescanMinutes` | `5` | How often the folder is checked |

Calls to Ollama (chat and embeddings) may take up to `AiProviders:OllamaTimeoutSeconds` (default `180`): a small model on
a laptop CPU can need over a minute to read a prompt with reference documents. Gemini and OpenAI keep
`AiProviders:TimeoutSeconds` (default `60`).

**Known limitation of small local models.** With `llama3.2:3b` the right document sections reach the model, but it
sometimes picks a wrong tool for a documentation question (for example offering a profile update when asked what TFA
can do), or sends the user to an administrator for something the documents simply do not cover. `llama3.1:8b` or Gemini
answer better. The measured results are in `openspec/changes/tfa-document-rag/verification.md`.

The chat model's context window for Ollama is set by `AiProviders:OllamaContextTokens` (default `8192`), so the
reference text is not cut off. In a container, mount the documents folder and point `DocumentKnowledge__SourcePath` at
it, for example `-v "/path/to/TF - Documents:/docs:ro" -e DocumentKnowledge__SourcePath=/docs`.

**"My manager"** is the creator of the task, when that is someone else. Otherwise it is the single Manager in your
department. If there is none, or more than one, TFA asks you to choose.

### Setting up the AI models (Administrator)

Open **Settings → TFA Settings** (Administrator only).

- The **Model** dropdown lists the models kept in the database, grouped by server (Ollama, Google Gemini, OpenAI GPT).
  Choose **Add new model…** to type one that is not in the list: it is saved to the database and offered from then on.
  A model you added can be taken out again with **Remove this model from the list** (not the built-in ones, and not the
  one saved for its server).
- The **Server** dropdown lists where each model runs: your Ollama address, or Google's or OpenAI's server. The two
  dropdowns are linked: choosing a model selects its server, and choosing a server shows that server's model.
- **The model and server you choose and save is the only one TFA offers.** The screen says "TFA uses …" for what is
  saved and "After you save, TFA will use …" for a change not saved yet. Saving switches the other servers off; their
  models and keys stay stored. With one model there is nothing to switch to, so the TFA window just shows it.
- Below them are only the fields of the chosen server: the **server address** for Ollama, the **API key** for Gemini and
  OpenAI, and **Test connection**. Test connection is always available: unsaved changes are saved first.
- Click **Save TFA settings**, then **Test connection**.

**First run.** The database is created, migrated and filled the first time the API starts, on any machine, with no
manual step. For TFA that means the list of models (Ollama, Gemini and OpenAI models) and one row per provider:
**Ollama is switched on** with `llama3.2:3b` at `http://localhost:11434`, and Gemini and OpenAI are off with no key. So
TFA works as soon as Ollama and the model are installed (see the setup script below), and nothing has to be configured.
This applies to a brand-new installation only. An installation that already has users keeps all three providers off
until an administrator turns one on in TFA Settings.
Starting again never overwrites what an administrator saved. The built-in model names are in `AiProviderCatalog.cs`
(`BuiltInModels`): add a name there to have it offered on every new installation. A model added in Settings is kept
in that installation's database only.

**API keys are never in the code or in git.** They are typed in Settings, encrypted, and kept in the database only, so
each installation enters its own.

| Model | Cost | Where it runs | Setup |
|---|---|---|---|
| **Ollama** | Free | On your own server: nothing leaves it | Run the setup script below (installs Ollama and the `llama3.2:3b` model), and keep the address `http://localhost:11434` |
| **Google Gemini** | Free tier, with rate limits | Google's cloud | Create a key in Google AI Studio and paste it |
| **OpenAI GPT** | **No free tier**: needs a paid key | OpenAI's cloud | Paste a paid API key |

#### Free local AI in one command (Ollama)

The model itself is not stored in this repository (it is about 2 GB). A setup script downloads it instead. After
cloning, run it once from the repository root:

```powershell
# Windows (PowerShell)
.\scripts\setup-ai.ps1
```

```bash
# macOS / Linux
./scripts/setup-ai.sh
```

The script installs Ollama if it is missing (winget on Windows, Homebrew on macOS, the official installer on Linux after
you confirm), starts the Ollama server, downloads `llama3.2:3b` and the document embedding model `nomic-embed-text`
(about 275 MB) if they are not already there, and checks that both answer. It is safe to run again. To use a different model, pass its name: `.\scripts\setup-ai.ps1 -Model llama3.1:8b`
or `./scripts/setup-ai.sh llama3.1:8b`. A larger model answers better but needs more memory (`llama3.1:8b` wants about
8 GB free).

Then, as an Administrator, open **Settings → TFA Settings**, choose **Ollama**, check the
model name matches what you installed (`llama3.2:3b` by default), and click **Test connection**. If Test connection says
the model is not available, the name in Settings is not one of the models Ollama has: run `ollama list` to see them.

The TFA window offers the model the administrator chose in TFA Settings. If more than one model is available (for example
an older installation that has several switched on), users switch from the selector in the TFA header without closing the
chat. Cloud models are marked
**Cloud**, and a user is told once that their message, related task details and matching TaskFlow document text are sent to that provider. TFA never
switches model by itself: if a model fails (rate limit, wrong key, Ollama not running, model not installed), the chat
explains what happened and the user chooses another one.

**API keys** are set only here and are stored encrypted on the server. After saving, a key is never shown again, only
"key set, ends …1234", and it is never written to the logs. Leave the key box empty to keep the stored key.

The encryption key ring is stored in the folder set by `AiProviders:KeyRingPath` in `appsettings.json` (default
`dp-keys`, under the data folder). On Windows the key ring is protected with DPAPI, so it only decrypts for the Windows
account that runs the API: a copied folder is not enough, and a restore on another machine or account means entering the
keys again. On other systems protect the folder on the file system. Keep it out of any backup that also holds the database.
If it is lost, the stored keys can no longer be read and must be entered again. Never commit it.

**Existing installs:** DPAPI only protects keys created after it was turned on, so a key ring made earlier keeps one
plain (unprotected) key. At startup on Windows the API logs a warning when it finds one. To protect it, stop the API,
delete the `C:\TaskFlow\dp-keys` folder, start the API, and enter the AI provider API keys again in
**Settings → TFA Settings**. Do this after the first time you run a build with DPAPI.

The Ollama server address cannot be a link-local or cloud metadata address (for example `169.254.169.254`).

### Voice

Voice has two parts, both for Chrome and Edge:

- **Hands-free** is on by default while you are signed in. Say **"Hey TF assistant"** at any time: the panel opens on
  the right, TFA greets you by name ("Hi Asha, how can I help you?"), and then listens for your command. If you say the
  command in the same breath ("Hey TF assistant, what is due today?"), it skips the greeting. You can turn hands-free
  off with **Turn off** in the panel, and TaskFlow remembers that.
- **The mic button** listens once, without the wake phrase. Press it, say your command, and it closes by itself.

Either way your words appear in the message box as you speak. A long command is collected piece by piece, and it is sent
after you have been quiet for about two seconds, so a short pause to think does not cut it off. TFA then answers in
the chat **and reads the answer aloud** (long answers are read in part; the rest is on screen). Typed questions get a
written answer only. The microphone rests while TFA is speaking, so it does not hear itself.

While the panel is closed, the **TFA button** shows whether the microphone is listening and, above it, the last words it
heard. If "Hey TF assistant" does nothing, that line shows whether the microphone is off, blocked, or hearing something
different from what you said.

The browser asks for microphone permission the first time. After a page reload it may need one click before it lets the
page listen again; the TFA button then says "Click anywhere on the page to turn "Hey TF assistant" back on". A microphone
error stops voice for that visit only and is never saved as your choice, so the next visit tries again. Voice uses the
browser's speech recognition and speech output, and Chrome processes the audio on Google's servers. Typing always works.

## Backend structure

The API is one project, `TaskFlow Proj/backend/src/TaskFlow.Api`, organised by layer first. Every layer uses the same
module tree, which follows the permission catalogue (the modules on the Roles screen):

```
TaskFlow.Api/
  Controllers/<Module>/                    HTTP only: routing, model binding, status codes (no sub-module folders)
  Services/<Module>/<SubModule>/[<Pair>/]  I<Name>Service.cs + <Name>Service.cs, one pair per folder (rules in Rules/)
  Repositories/<Module>/<SubModule>/[<Pair>/]
                                           I<Name>Repository.cs + <Name>Repository.cs over EF Core, one pair per folder;
                                           IBaseRepository, BaseRepository, IUnitOfWork and UnitOfWork in Base/
  Dtos/<Module>/<SubModule>/               requests, responses, Validators/
  Models/Entities/  Models/Enums/          database entities and enums
  Integrations/<Area>/<Pair>/              AI providers, JWT, TOTP, key protection, file storage (interface beside class)
  Middleware/  Filters/  Authorization/    exception handling, request logging, session validation, MFA gate,
                                           request validation, permission policies
  Config/                                  DI registration per layer, options classes
  Common/                                  exceptions, security keys and policies, shared rules
  Persistence/                             AppDbContext, migrations, seeders
```

Modules: `Auth` (`Mfa`, `PasswordRecovery`), `Dashboard` (`Analytics`), `Monitoring` (`TaskManagement` with
`Discussion` and `Activity`, `Board` (frontend only), `EmployeeWorkload`), `Management` (`EmployeeManagement` with `Allocations`,
`ProjectManagement` with `Positions`, `DepartmentManagement`), `Communication` (`Mail` with `Attachments`,
`Notification` with `Reminders`), `Reports`, `Settings` (`ApplicationSettings`, `ReminderSettings`, `RoleConfiguration`,
`TfaSettings`), `Profile` and `Assistant`.

Layer rules, checked by `tests/TaskFlow.UnitTests/Architecture/ArchitectureTests.cs` on every test run:

- A request flows Controller → Service → Repository → database, and a layer only calls the one below it, through its
  interface. Controllers never reach repositories, integrations or EF Core.
- Only `Persistence`, `Repositories` and the startup code touch `AppDbContext`. Repositories never return `IQueryable`
  and never save: the service that owns the use case commits through `IUnitOfWork`.
- Controllers never take or return entities, never validate requests themselves (the global `ValidationFilter` runs the
  FluentValidation validators) and never catch exceptions. Services throw `AppException` subclasses
  (`BusinessRuleException` 400, `ForbiddenException` 403, `NotFoundException` 404, `ConflictException` 409,
  `RateLimitedException` 429), and `GlobalExceptionHandler` turns them into ProblemDetails.
- Each file's namespace matches its folder, each layer folder holds only module paths, classes are named for their
  layer (`*Controller`, `*Service`, `*Repository`) and there are no empty folders.
- Controllers sit directly in their top-level module folder (`Controllers/Auth/PasswordRecoveryController.cs`).
- Every interface has a file of its own named after it, in the same folder as the class that implements it
  (`Services/Profile/IProfileService.cs` next to `Services/Profile/ProfileService.cs`). There is no `Interfaces` folder,
  and an interface file declares nothing but its interface: the records it uses each have a file of their own beside it.
  `Authorization/` follows the same rule (`Authorization/Assistant/`, `TaskAccess/`, `Policies/`, `Auditing/`).
- Every service and repository has a folder of its own. When a module has more than one, or also has sub-modules, each
  pair gets a named folder: `Services/Auth/Login/` (`IAuthService`, `AuthService`), `Services/Auth/Sessions/`,
  `Services/Auth/Mfa/`. A folder holds either code or sub-folders, never both; a module's shared `Rules/` folder and the
  assistant's `Knowledge/` help topics are the only exceptions. Folders are named for what they hold, never after a
  type, so a folder never hides a type name (`Tasks/`, not `Task/`).

The test projects (`tests/TaskFlow.UnitTests`, `tests/TaskFlow.IntegrationTests`) use the same folders as the code
they test, for example `Auth/Login/AuthServiceLoginTests.cs`, with namespaces that match. Tests that belong to no module sit in
`Architecture/`, `Common/`, `Persistence/` or `Integrations/`. Fixtures and fakes shared across modules are in
`TestSupport/`, which `GlobalUsings.cs` imports everywhere.

## Frontend structure

The Angular app, `TaskFlow Proj/frontend/src/app`, uses the same module and sub-module tree as the backend, in
kebab-case (`Management/EmployeeManagement` -> `modules/management/employee-management`):

```
src/app/
  modules/<module>/<sub-module>/     one folder per backend module; auth and legal are frontend-only (they run before sign-in)
    pages/                           routed screens (*.component.ts/html/css)
    components/                      non-routed pieces of the screens
    services/                        API services (*.service.ts) for that module's endpoints
    models/                          the module's types (*.model.ts)
    utils/                           pure helpers used only by that module
  modules/<module>/<module>.routes.ts  the module's routes, loaded by routes/app.routes.ts with loadChildren
  shared/                            code used by two or more modules: components/, directives/, services/, utils/,
                                     auth/ (guards, interceptor, auth service, token store, permission keys), testing/
  layouts/                           the app shell, its navigation and the theme menu
  routes/                            the root route table
  config/                            app providers, the API base URL and its interceptor
src/assets/                          static files (favicon)
```

Modules: `auth` (`login`, `account-recovery`, `security-setup`), `legal`, `dashboard` (`analytics`), `monitoring`
(`task-management` with `discussion` and `activity`, `board`, `employee-workload`), `management` (`employee-management`,
`project-management` with `positions`, `department-management`), `communication` (`mail`, `notification`), `reports`,
`settings` (`application-settings`, `reminder-settings`, `role-configuration`, `tfa-settings`), `profile` and `assistant`.

Placement rules, checked by `npm run check:structure` (also the first step of `npm test`):

- A file sits in the layer folder for its kind; only `<module>.routes.ts` sits directly in a module folder.
- An API service lives in the module whose endpoints it calls, even when other modules import it.
- A component's or service's own types go in a `<name>.model.ts` beside it; types shared by a module go in its `models/`.
- Code moves to `shared/` only when two or more modules, or the app shell, use it. Spec files sit next to the file they test.
- A route's `loadComponent` target is always in a `pages/` folder.
- There are no empty folders, and the old `core/` and `features/` folders are gone.


---

## Copyright

© 2026 Jojo George (jojogeorge3344@gmail.com). All rights reserved.

TaskFlow, its source code, screens and documentation are the work of the author, built as a personal project to
develop and demonstrate software engineering skills. Unauthorized copying, distribution or modification of this software or its documentation, in whole or in part,
is prohibited without the author's written permission. Third-party libraries remain under their own licences (see the
**Licences** page in the application).
