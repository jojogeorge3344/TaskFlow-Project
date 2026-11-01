/**
 * The single source of truth for who owns TaskFlow, who to contact about it and what the
 * application is called.
 *
 * The five legal documents, the copyright footer and the metadata published in the document
 * head all read these values, so the ownership statement cannot drift between them. The document
 * registry never writes an owner name, a year or a contact address out in full: it uses the
 * placeholder tokens below, and they are substituted when a document renders.
 */
export const LEGAL_OWNER = {
  /** The copyright holder. */
  name: 'Jojo George',
  /** The year the work is claimed under. */
  year: 2026,
  /** The address for permission requests, privacy enquiries and complaints. */
  email: 'jojogeorge3344@gmail.com',
  /** The rights statement that follows the ownership line. */
  rights: 'All Rights Reserved'
} as const;

/** What the application is called, in the two forms it is used in. */
export const LEGAL_APPLICATION = {
  /** The short name, used in navigation and in the footer. */
  name: 'TaskFlow',
  /** The full title, used in the document head and in the legal documents. */
  title: 'Intelligent Task Allocation & Tracking System',
  /** The one-line description published in the document head. */
  description:
    'TaskFlow allocates, tracks and reports on organisational work across departments, with deadline reminders and a built-in assistant.'
} as const;

/**
 * The placeholders the document registry uses in place of the values above. They are written
 * into the prose as literal `{{token}}` text and replaced at render time.
 */
export const LEGAL_TOKENS = {
  owner: '{{owner}}',
  year: '{{year}}',
  email: '{{email}}',
  rights: '{{rights}}',
  applicationName: '{{applicationName}}',
  applicationTitle: '{{applicationTitle}}'
} as const;

const TOKEN_VALUES: Readonly<Record<string, string>> = {
  [LEGAL_TOKENS.owner]: LEGAL_OWNER.name,
  [LEGAL_TOKENS.year]: String(LEGAL_OWNER.year),
  [LEGAL_TOKENS.email]: LEGAL_OWNER.email,
  [LEGAL_TOKENS.rights]: LEGAL_OWNER.rights,
  [LEGAL_TOKENS.applicationName]: LEGAL_APPLICATION.name,
  [LEGAL_TOKENS.applicationTitle]: LEGAL_APPLICATION.title
};

/**
 * Replaces every placeholder in `text` with the value it stands for. Used by the legal page
 * renderer so the prose in the registry never has to repeat the owner's details.
 */
export function resolveLegalTokens(text: string): string {
  let resolved = text;
  for (const [token, value] of Object.entries(TOKEN_VALUES)) {
    resolved = resolved.replaceAll(token, value);
  }
  return resolved;
}

/** The ownership line shown in the footer and at the foot of the legal pages. */
export function legalOwnershipLine(): string {
  return `© ${LEGAL_OWNER.year} ${LEGAL_OWNER.name}. ${LEGAL_OWNER.rights}.`;
}
