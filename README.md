# TaskFlow

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
default is `../database/mail-attachments`, resolved from the backend folder like the database, so it sits next to
`database/taskflow.db`. Only the uploader and people who have the mail in their mailbox can download a file.
**Back up this folder together with the database.**

### Deadline reminders in Mail

Every automatic deadline reminder also arrives in the recipient's Inbox as a mail from **TaskFlow**, with a link to
the task. These mails cannot be replied to, but they can be forwarded. To turn this off, clear
**Reminder Settings → Also deliver reminders to Mail**. Bell notifications are always sent either way.

`Mail:AppBaseUrl` sets the address used for the task link (default `http://localhost:4200`).

## TFA, the AI assistant

TFA is a chat assistant in a small panel on the right of every signed-in screen. Open it with the **TFA** button, by
typing, or by saying **"Hey TFA"**. It answers how-to questions about TaskFlow, tells you what is upcoming or overdue,
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

Open **Settings → Application Settings → AI assistant (TFA)**. Turn on at least one model, then use **Test connection**.
Nothing is enabled by default, and TFA says it is not available until you do.

| Model | Cost | Where it runs | Setup |
|---|---|---|---|
| **Ollama** | Free | On your own server: nothing leaves it | Install Ollama, run `ollama pull llama3.1:8b`, and keep the address `http://localhost:11434` |
| **Google Gemini** | Free tier, with rate limits | Google's cloud | Create a key in Google AI Studio and paste it |
| **OpenAI GPT** | **No free tier**: needs a paid key | OpenAI's cloud | Paste a paid API key |

Users switch model from the selector in the TFA header at any time, without closing the chat. Cloud models are marked
**Cloud**, and a user is told once that their message and related task details are sent to that provider. TFA never
switches model by itself: if a model fails (rate limit, wrong key, Ollama not running, model not installed), the chat
explains what happened and the user chooses another one.

**API keys** are set only here and are stored encrypted on the server. After saving, a key is never shown again, only
"key set, ends …1234", and it is never written to the logs. Leave the key box empty to keep the stored key.

The encryption key ring is stored in the folder set by `AiProviders:KeyRingPath` in `appsettings.json` (default
`../database/dp-keys`). On Windows the key ring is protected with DPAPI, so it only decrypts for the Windows
account that runs the API: a copied folder is not enough, and a restore on another machine or account means entering the
keys again. On other systems protect the folder on the file system. Keep it out of any backup that also holds the database.
If it is lost, the stored keys can no longer be read and must be entered again. Never commit it.

The Ollama server address cannot be a link-local or cloud metadata address (for example `169.254.169.254`).

### Voice

Voice is **off until you turn it on** with the microphone button in the panel. While it is on, the panel shows that it
is listening. Say "Hey TFA", then your question: it appears in the message box and is sent only when you press Send.
Voice uses the browser's speech recognition, so it works in **Chrome and Edge** only, and Chrome processes the audio on
Google's servers. Typing always works.
