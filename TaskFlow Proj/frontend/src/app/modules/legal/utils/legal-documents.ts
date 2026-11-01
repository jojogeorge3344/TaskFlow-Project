import { LegalDocument } from '../models/legal-document.model';
import { LEGAL_TOKENS } from './legal-owner';

const { owner, year, email, rights, applicationName, applicationTitle } = LEGAL_TOKENS;

/**
 * The five published legal documents. Every place that names the owner, the year or the contact
 * address uses placeholder tokens, substituted at render time from `LEGAL_OWNER` (a single
 * source of truth). The content below is written against the implementation that exists today
 * and calls out the claims that were re-checked against the code where relevant.
 */
export const LEGAL_DOCUMENTS: LegalDocument[] = [
  {
    id: 'copyright',
    title: 'Copyright Notice',
    navLabel: 'Copyright',
    summary: 'Who owns {{applicationName}}, what this notice protects and how you may use it.',
    updated: '21 October 2026',
    sections: [
      {
        id: 'ownership',
        heading: 'Ownership',
        blocks: [
          {
            kind: 'paragraph',
            text: `{{applicationName}} — the {{applicationTitle}} — is copyright © {{year}} {{owner}}. {{rights}}. No permission is granted to copy, modify or redistribute the software except as set out in this notice.`
          },
          {
            kind: 'paragraph',
            text: `The owner can be contacted at {{email}}.`
          }
        ]
      },
      {
        id: 'protected-work',
        heading: 'What this notice protects',
        blocks: [
          {
            kind: 'list',
            items: [
              'The source code of the application, including the front end, the API, the database design and the build and deployment scripts.',
              "The visual design of the application: its layout, screens, icons, colour scheme and the arrangements of its components.",
              'The written material in the application, including the on-screen help, the generated reports and the documentation published with it.',
              'The name of the application and the name of its assistant.',
              'The compiled database schema and the seed data written for this installation.'
            ]
          }
        ]
      },
      {
        id: 'not-protected',
        heading: 'What this notice does not cover',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The following belong to their own owners and are used under their own licences; {{applicationName}} does not claim them and does not restrict their use:'
          },
          {
            kind: 'list',
            items: [
              'Third-party open-source components listed in the Licence notices, each under its own licence.',
              'The icon paths drawn from the Lucide icon set, used under the ISC licence.',
              "Model names and marks belonging to Google, OpenAI, Meta or other providers, used only to identify the services an administrator connects to."
            ]
          },
          {
            kind: 'paragraph',
            text: 'A trade mark is a different right from copyright. Nothing in this notice grants a trade mark licence. Where the owner uses a name or mark, that use is descriptive and does not imply a partnership, endorsement or sponsorship.'
          }
        ]
      },
      {
        id: 'permitted-use',
        heading: 'What you may do',
        blocks: [
          {
            kind: 'list',
            items: [
              'Install, run and use the application for your own work, including for study, research and internal business use.',
              'View the screens, print the reports and save copies of the material the application produces for your own records.',
              'Keep a personal backup of the application and of the data it holds on your own machine.',
              'Quote short extracts from the on-screen documents for the purpose of review, commentary or teaching, provided the owner is credited.'
            ]
          }
        ]
      },
      {
        id: 'prohibited-use',
        heading: 'What you may not do',
        blocks: [
          {
            kind: 'list',
            items: [
              'Copy, publish or distribute the source code, the compiled application or any substantial part of them, whether modified or not.',
              'Sell, rent, sublicense or otherwise make the software available for money.',
              "Remove or obscure the copyright notice, the ownership statement or any other proprietary notice from the application or its materials.",
              'Use the application, or any part of it, to build a competing product or a service offered to third parties.',
              'Reverse engineer, decompile or disassemble the software except where that restriction is not permitted by the law that applies to you.'
            ]
          }
        ]
      },
      {
        id: 'credit',
        heading: 'How to credit the work',
        blocks: [
          {
            kind: 'paragraph',
            text: 'When you describe or demonstrate the application, please say that it is {{applicationName}}, the {{applicationTitle}}, copyright © {{year}} {{owner}}, and quote the notice as it appears at the foot of every screen.'
          }
        ]
      },
      {
        id: 'permission',
        heading: 'Requesting permission',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Permission to copy, modify or redistribute is granted only in writing by the owner. Write to {{email}} with what you intend to do, the material you want to use and the context. A request is answered on a best-effort basis and a permission given for one use does not extend to another.'
          }
        ]
      },
      {
        id: 'enforcement',
        heading: 'Enforcement',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The owner may take legal action against any use of the software that is not permitted by this notice or by the terms of use.'
          }
        ]
      }
    ]
  },
  {
    id: 'terms',
    title: 'Terms of Use',
    navLabel: 'Terms of Use',
    summary: 'The conditions on which you may use {{applicationName}}.',
    updated: '21 October 2026',
    sections: [
      {
        id: 'acceptance',
        heading: 'Acceptance of these terms',
        blocks: [
          {
            kind: 'paragraph',
            text: 'These terms apply to every use of {{applicationName}}, including the application itself, the on-screen documents and the assistant. By opening, installing or using the application you accept these terms. If you do not accept them, do not use the application.'
          },
          {
            kind: 'paragraph',
            text: 'The owner may update these terms. The version on screen is dated, and the dated version is the one that applies to your use from the moment it is published.'
          }
        ]
      },
      {
        id: 'licence',
        heading: 'Licence to use the application',
        blocks: [
          {
            kind: 'paragraph',
            text: "The owner grants you a personal, non-exclusive, non-transferable, revocable licence to install and use the application for the purposes the terms allow. The licence is granted for your own use; it does not let you resell the application, share it as a service or use it to operate a business that competes with the owner."
          },
          {
            kind: 'paragraph',
            text: 'The licence ends immediately if you breach these terms. On it ending you must stop using the application and delete any copy you hold.'
          }
        ]
      },
      {
        id: 'accounts',
        heading: 'Your account',
        blocks: [
          {
            kind: 'list',
            items: [
              'The application is used by named people within an organisation. An account is issued to a person and is not transferable.',
              'You are responsible for the credentials you use and for everything done under your account. Tell the administrator promptly if you believe your credentials have been misused.',
              'The administrator controls the role your account holds and the screens that role opens. The permissions applied to your role are enforced by the server, so a change applies to your next action without you signing in again.',
              'You must be old enough to enter a contract in the jurisdiction where you live to hold an account.'
            ]
          }
        ]
      },
      {
        id: 'acceptable-use',
        heading: 'Acceptable use',
        blocks: [
          {
            kind: 'paragraph',
            text: 'You agree not to use the application to do anything unlawful or to cause harm to the person who runs it or to anyone else. In particular you agree not to:'
          },
          {
            kind: 'list',
            items: [
              'Use the application to store or distribute unlawful content, or content that infringes the rights of others.',
              'Send internal mail to someone who is not a user of the application, or use it to send material you were not asked to send.',
              "Attempt to reach a screen or a record your role does not open, or to work around the permission checks that protect it.",
              'Probe, scan or test the application for weaknesses without the owner\'s written permission.',
              'Reverse engineer, decompile or disassemble the application except where the law that applies to you does not allow that restriction.',
              'Misrepresent your identity or your role, or lend your credentials to another person.'
            ]
          }
        ]
      },
      {
        id: 'your-content',
        heading: 'Your content',
        blocks: [
          {
            kind: 'paragraph',
            text: 'You keep every right you already had in the tasks, comments, mail and reports you enter into the application. You grant the owner a non-exclusive, worldwide, royalty-free licence to store, reproduce, display and report on that content for the purpose of running the application and providing the features it offers. That licence ends when you delete the content, except for copies the application must keep for its own operation, such as an activity record.'
          },
          {
            kind: 'paragraph',
            text: '{{applicationName}} makes no claim over the content you enter beyond the licence above, and its owner claims no ownership of it.'
          }
        ]
      },
      {
        id: 'intellectual-property',
        heading: 'Intellectual property',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The application, its design and the material in it are owned by {{owner}} and are protected by copyright. Your use of the application is governed by the copyright notice and by these terms. You get no rights in the names of the application or of its assistant beyond the right to use them to refer to it truthfully.'
          }
        ]
      },
      {
        id: 'third-party-ai',
        heading: 'Third-party AI providers',
        blocks: [
          {
            kind: 'paragraph',
            text: "The application can answer questions through an assistant. An administrator chooses which model the assistant uses. When a model that runs on the provider's own servers is selected, the text of your question and the task details behind it are sent to that provider so that it can answer. When a model running on your own machine is selected, the same content stays on that machine."
          },
          {
            kind: 'paragraph',
            text: "The owner does not control a third-party provider's servers, its retention practices or its own terms. Review the provider's terms before selecting it. The application shows you a notice the first time you use a cloud model."
          }
        ]
      },
      {
        id: 'speech',
        heading: 'Speech recognition',
        blocks: [
          {
            kind: 'paragraph',
            text: "The assistant can be spoken to. Speech recognition is performed by your browser, which sends the audio to the browser vendor's service to turn it into text. The audio is processed by that vendor, not by the owner of the application, and the application keeps no recording of it. Speech recognition is available in Chrome and Edge."
          }
        ]
      },
      {
        id: 'suspension',
        heading: 'Suspension and termination',
        blocks: [
          {
            kind: 'paragraph',
            text: 'You may stop using the application at any time and delete your account by asking the administrator. The owner or an administrator may suspend or close an account that breaches these terms, that endangers the security of the application or that is required by law.'
          }
        ]
      },
      {
        id: 'warranty',
        heading: 'Disclaimer of warranties',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The application and the material published with it are provided "as is" and "as available", without warranty of any kind, whether express or implied, including any warranty of accuracy, availability, fitness for a particular purpose, non-infringement or fitness for a particular purpose. The owner does not warrant that the application will be uninterrupted, error-free or fit for any particular purpose, and no statement in the documentation or in the assistant is a warranty. The full statement is in the disclaimer.'
          }
        ]
      },
      {
        id: 'liability',
        heading: 'Limitation of liability',
        blocks: [
          {
            kind: 'paragraph',
            text: "To the fullest extent the law allows, the owner and the people who operate the application for the owner are not liable for any loss arising out of or in connection with the use of the application or the inability to use it, including loss of data, lost profit, lost revenue, loss of business, loss of goodwill or business interruption, and whether the loss is direct or indirect. Nothing in these terms excludes liability that cannot lawfully be excluded."
          }
        ]
      },
      {
        id: 'indemnity',
        heading: 'Indemnity',
        blocks: [
          {
            kind: 'paragraph',
            text: 'You agree to indemnify the owner against any claim, loss, damage, cost or expense arising out of your use of the application, your breach of these terms, or your infringement of the rights of anyone else, to the extent the law allows it.'
          }
        ]
      },
      {
        id: 'changes',
        heading: 'Changes to these terms',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The owner may change these terms. The date at the foot of this document shows when it was last updated. Continuing to use the application after a change means you accept the changed terms. If you do not accept them, stop using the application and ask the administrator to close your account.'
          }
        ]
      },
      {
        id: 'governing-law',
        heading: 'Governing law',
        blocks: [
          {
            kind: 'paragraph',
            text: 'These terms are governed by the laws of India. Any dispute arising from them or from the use of the application shall be subject to the exclusive jurisdiction of the courts at the owner\'s place of residence in India, and the parties submit to that jurisdiction.'
          }
        ]
      }
    ]
  },
  {
    id: 'privacy',
    title: 'Privacy Policy',
    navLabel: 'Privacy Policy',
    summary: 'What {{applicationName}} collects, why, where it is kept and who may see it.',
    updated: '21 October 2026',
    sections: [
      {
        id: 'who-we-are',
        heading: 'Who we are',
        blocks: [
          {
            kind: 'paragraph',
            text: '{{owner}} is the owner and operator of {{applicationName}}, the {{applicationTitle}}. This policy explains what the application processes, why it processes it and where the data goes. It applies to every installation of the application, including the one running on your own organisation\'s servers.'
          },
          {
            kind: 'paragraph',
            text: 'Questions about this policy, or a request to act on your data, can be sent to {{email}}.'
          }
        ]
      },
      {
        id: 'data-categories',
        heading: 'What we collect',
        blocks: [
          {
            kind: 'table',
            caption: 'The categories of data {{applicationName}} processes, and why each is processed.',
            head: ['Data', 'What it covers', 'Why it is processed'],
            rows: [
              [
                'Account data',
                'Full name, username, work email address, role, employee code, department and a hashed password.',
                'To identify you, to let you sign in, and to decide which screens and actions your role opens.'
              ],
              [
                'Task data',
                'Task title, description, status, priority, deadline, estimate, assignee, creator, comments and related records.',
                'To allocate, track and report on the work the organisation asks for.'
              ],
              [
                'Mail data',
                'Messages you send and receive, and the files attached to them.',
                'To provide the internal mail the application offers. Mail is stored as per-user copies; administrators have no access to other users\' private mail.'
              ],
              [
                'Notification and reminder records',
                'Which reminders were raised, when they were sent and when they were read.',
                'To run the deadline reminder schedule and to show what a person has been told. Reminders can be delivered to internal {{applicationName}} Mail.'
              ],
              [
                'Activity history',
                'A record of what happened to a task, such as a status change, a reassignment or a comment.',
                'To let a person see what has happened to the work they are responsible for.'
              ],
              [
                'Application settings',
                'The application name, the default timezone, the default task priority and the reminder configuration.',
                'To let an administrator configure the behaviour of the installation.'
              ],
              [
                'Assistant conversations',
                'The messages you send to the assistant and the replies it returns.',
                'To answer your question and to keep a record of what the assistant was asked.'
              ],
              [
                'Provider credentials',
                'The API keys an administrator enters for a cloud model provider.',
                'To let the installation call that provider. The keys are encrypted before they are stored.'
              ]
            ]
          }
        ]
      },
      {
        id: 'storage',
        heading: 'Where data is stored',
        blocks: [
          {
            kind: 'paragraph',
            text: '{{applicationName}} operates on the machine that runs the server. The SQLite database, mail attachments and the encrypted AI key ring are stored in the local {{applicationName}} data folder on that machine (by default on Windows, C:\\TaskFlow; elsewhere under the machine\'s shared application data). No hosted copy of that data is sent by {{applicationName}} to the owner.'
          },
          {
            kind: 'note',
            text: 'Storage location checked against the implementation: StorageRoot default (Windows) is C:\\TaskFlow, database is taskflow.db, attachments go in mail-attachments, and the AI key ring is stored under the dp-keys folder.'
          }
        ]
      },
      {
        id: 'ai-provider',
        heading: 'External AI providers',
        blocks: [
          {
            kind: 'paragraph',
            text: "When an administrator selects a cloud model, the text of your question and the task details behind it are sent to that provider so it can answer. When a model running on your own machine is selected, the same content stays on that machine. {{applicationName}} shows a notice the first time you use a cloud model."
          },
          {
            kind: 'paragraph',
            text: "The owner does not control a third-party provider's servers, its retention practices or its own terms. Review the provider's terms before selecting it."
          }
        ]
      },
      {
        id: 'browser-speech',
        heading: 'Browser speech recognition',
        blocks: [
          {
            kind: 'paragraph',
            text: "Spoken commands are recognised by your browser, which sends the audio to the browser vendor's service to turn it into text. The audio is processed by that vendor, not by the owner of the application, and {{applicationName}} stores no audio recording. Speech recognition is available in Chrome and Edge."
          }
        ]
      },
      {
        id: 'mail-privacy',
        heading: 'Who can see what',
        blocks: [
          {
            kind: 'paragraph',
            text: "Each person sees only their own mail. Access is enforced per mailbox item (scoped to the owner of the item); administrators have no access to other users' private mail. Task, workload and report data are shown according to the permissions assigned to a role. Audit records of assistant actions do not include message bodies or API keys."
          },
          {
            kind: 'note',
            text: 'Checked against MailService (mailbox items are queried by OwnerUserId) and the model — private mail is per-user copies; AssistantAudit stores action metadata, not message bodies or keys.'
          }
        ]
      },
      {
        id: 'security',
        heading: 'Security measures',
        blocks: [
          {
            kind: 'list',
            items: [
              // Checked against Program.cs: PasswordHasher<User> is registered (default PBKDF2/HMAC-SHA256)
              'Passwords are stored only as a salted PBKDF2 hash (HMAC-SHA256) produced by the application\'s password hasher.',
              // Checked against Program.cs: AddDataProtection + ProtectKeysWithDpapi; AI keys encrypted with Data Protection
              'AI provider API keys are encrypted at rest with ASP.NET Data Protection (protected keys on Windows).',
              // Checked against MailService/storage rules and models
              'Attachments are stored under generated identifiers and enforced by attachment type and size limits.',
              'Session tokens use refresh tokens and a security stamp that invalidates older tokens when credentials change.'
            ]
          },
          {
            kind: 'note',
            text: 'Password hasher registration confirmed in Program.cs (AddScoped<IPasswordHasher<User>, PasswordHasher<User>>); Data Protection configured with DPAPI key protection in Program.cs.'
          }
        ]
      },
      {
        id: 'no-sale-tracking',
        heading: 'Sale, analytics and advertising',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Personal data is not sold. {{applicationName}} embeds no third-party analytics or advertising trackers, and personal data is not used to build advertising profiles.'
          }
        ]
      },
      {
        id: 'retention',
        heading: 'Retention and deletion',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Data is retained for as long as the account or the installation needs it to provide the features of {{applicationName}}. When content is deleted from the product, copies required for normal operation (such as activity records) may be kept for a time; mail items that have been purged are marked but may remain until removed by administrative processes.'
          }
        ]
      },
      {
        id: 'rights',
        heading: 'Your rights and contact',
        blocks: [
          {
            kind: 'paragraph',
            text: 'You may ask the administrator for a copy of the data held about you, to correct it, or to delete your account where the law allows it. To make a request, write to {{email}}. {{applicationName}} keeps no hosted service that receives this data, so requests are answered by the people who operate this installation.'
          }
        ]
      },
      {
        id: 'children',
        heading: 'Children',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The application is intended for use in a workplace or organisational setting and is not designed for children. If you believe a child is using it, please let the administrator know.'
          }
        ]
      }
    ]
  },
  {
    id: 'disclaimer',
    title: 'Disclaimer',
    navLabel: 'Disclaimer',
    summary: 'Limitations on warranties, advice and reliance on {{applicationName}}.',
    updated: '21 October 2026',
    sections: [
      {
        id: 'no-warranty',
        heading: '"As is" and no warranty',
        blocks: [
          {
            kind: 'paragraph',
            text: '{{applicationName}} and the material published with it are provided "as is" and "as available", without warranty of any kind, whether express or implied, including without limitation any warranty of accuracy, availability, fitness for a particular purpose, non-infringement or any other warranty. The owner does not warrant that the application will be uninterrupted, error-free or fit for any particular purpose.'
          },
          {
            kind: 'paragraph',
            text: 'Nothing in the application, its documents or its assistant is professional, legal, financial or medical advice. If you need such advice, speak to a qualified professional.'
          }
        ]
      },
      {
        id: 'assistant-output',
        heading: 'Assistant output',
        blocks: [
          {
            kind: 'paragraph',
            text: "Machine-generated replies from the assistant may be incomplete, incorrect, out of date or inappropriate. They are not a substitute for professional judgement. You must verify anything the assistant prepares — including facts, links, suggestions or actions — before you act on it. The owner is not responsible for decisions you make based on assistant output."
          }
        ]
      },
      {
        id: 'advisory-suggestion',
        heading: 'Prioritisation and allocation suggestions',
        blocks: [
          {
            kind: 'paragraph',
            text: 'Suggested priorities, what to work on first or recommended allocations are advisory only. A manager remains responsible for allocation decisions. The application does not automate a binding decision unless a specific feature states otherwise.'
          }
        ]
      },
      {
        id: 'availability',
        heading: 'Availability and changes',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The application may be unavailable from time to time, and features may change without notice. The owner may update the application or this disclaimer at any time.'
          }
        ]
      },
      {
        id: 'external-links',
        heading: 'External links',
        blocks: [
          {
            kind: 'paragraph',
            text: "Links to external sites are provided for convenience only. The owner has no control over those sites and accepts no responsibility for their content or their terms."
          }
        ]
      },
      {
        id: 'pre-release',
        heading: 'Pre-release status',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The software is provided as-is for evaluation and use as set out in its terms. No guarantee of support is implied.'
          }
        ]
      },
      {
        id: 'liability',
        heading: 'Limitation of liability',
        blocks: [
          {
            kind: 'paragraph',
            text: "To the fullest extent the law allows, the owner is not liable for any loss arising from the use of, or the inability to use, the application or its documents, including loss of data, business or profits. Nothing in this disclaimer excludes liability that cannot lawfully be excluded."
          }
        ]
      }
    ]
  },
  {
    id: 'licenses',
    title: 'Third-Party Licence Notices',
    navLabel: 'Licences',
    summary: 'TaskFlow\'s licence and attributions for third-party components.',
    updated: '21 October 2026',
    sections: [
      {
        id: 'taskflow-license',
        heading: '{{applicationName}} licence',
        blocks: [
          {
            kind: 'paragraph',
            text: '{{applicationName}} (the {{applicationTitle}}) — source code and compiled form — is copyright © {{year}} {{owner}}. {{rights}}. No permission to copy, modify, publish, distribute, sublicense or sell the software is granted.'
          },
          {
            kind: 'paragraph',
            text: 'The name {{applicationName}}, the TFA name and the TF mark are used under permission. No trade mark licence is granted, and other names may belong to their own owners.'
          }
        ]
      },
      {
        id: 'third-party-table',
        heading: 'Third-party components',
        blocks: [
          {
            kind: 'table',
            caption: 'Components used by {{applicationName}} and their licences.',
            head: ['Component', 'Version / usage', 'Licence'],
            rows: [
              ['Angular', '19 (standalone components, Angular Router)', 'MIT'],
              ['@angular/common / @angular/core / @angular/forms / @angular/platform-browser', '19.x', 'MIT'],
              ['RxJS', '7.x', 'Apache-2.0'],
              ['TypeScript', '5.7.x', 'Apache-2.0'],
              ['Zone.js', '0.15.x', 'MIT'],
              ['.NET', '10', 'MIT'],
              ['ASP.NET Core', '10', 'MIT'],
              ['Entity Framework Core', '10 (SQLite provider)', 'MIT'],
              ['Microsoft.Data.Sqlite / SQLite', 'Used by EF Core SQLite provider', 'MIT'],
              ['FluentValidation', 'Used for request validation', 'Apache-2.0'],
              ['System.IdentityModel.Tokens.Jwt / Microsoft.AspNetCore.Authentication.JwtBearer', 'JWT bearer authentication', 'MIT'],
              ['Lucide', 'Inline SVG icon paths used by the interface', 'ISC'],
              ['Ollama / local model runtime', 'Local model integration (when used)', 'MIT']
            ]
          }
        ]
      },
      {
        id: 'full-texts',
        heading: 'Full licence texts',
        blocks: [
          {
            kind: 'paragraph',
            text: 'The full licence texts that the notices above summarise are kept in THIRD-PARTY-NOTICES.md at the repository root.'
          },
          {
            kind: 'note',
            text: 'If a component is added or removed, update both this table and THIRD-PARTY-NOTICES.md.'
          }
        ]
      }
    ]
  }
];

/** Look up a document by id. */
export function getLegalDocument(id: string): LegalDocument | undefined {
  return LEGAL_DOCUMENTS.find((doc) => doc.id === id);
}

/** All legal document ids in the order they appear in the registry. */
export const LEGAL_DOCUMENT_IDS = LEGAL_DOCUMENTS.map((doc) => doc.id);
