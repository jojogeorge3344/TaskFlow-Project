# TaskFlow

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
  not the account exists, and no password is ever sent by email or text message.
- **Nobody can turn it off.** Two-step verification is a security layer, so there is no switch for it, not for users and not
  for administrators. If a phone and its backup codes are both lost, see the next bullet.
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
| `Mfa:Required` | `true` (default): nobody can use the app until two-step verification is set up. Set `Mfa__Required=false` only as a break-glass measure, for example if every Administrator is locked out, and turn it back on afterwards. |
| `Mfa:Issuer` | The name the authenticator app shows beside the account (default `TaskFlow`). |

The key is stored encrypted with the same key ring as the AI provider keys (`AiProviders:KeyRingPath`), so keep that
folder with the database in backups, or every user with MFA would need an administrator reset. Backup codes are stored
only as hashes. Nothing secret is logged.

## TFA, the AI assistant

TFA is a chat assistant in a small panel on the right of every signed-in screen. Open it with the **TFA** button, by
typing, or by saying **"Hey TF assistant"**. It answers how-to questions about TaskFlow, tells you what is upcoming or overdue,
suggests what to work on first, and drafts mail for you to review. Replies are written, in the same window.

### What TFA can do

| Ask | What happens |
|---|---|
| "How do I change a task's status?" | Answered from the written help for your role, with a link to the screen |
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
you confirm), starts the Ollama server, downloads `llama3.2:3b` if it is not already there, and checks that the model
answers. It is safe to run again. To use a different model, pass its name: `.\scripts\setup-ai.ps1 -Model llama3.1:8b`
or `./scripts/setup-ai.sh llama3.1:8b`. A larger model answers better but needs more memory (`llama3.1:8b` wants about
8 GB free).

Then, as an Administrator, open **Settings → TFA Settings**, choose **Ollama**, check the
model name matches what you installed (`llama3.2:3b` by default), and click **Test connection**. If Test connection says
the model is not available, the name in Settings is not one of the models Ollama has: run `ollama list` to see them.

The TFA window offers the model the administrator chose in TFA Settings. If more than one model is available (for example
an older installation that has several switched on), users switch from the selector in the TFA header without closing the
chat. Cloud models are marked
**Cloud**, and a user is told once that their message and related task details are sent to that provider. TFA never
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
