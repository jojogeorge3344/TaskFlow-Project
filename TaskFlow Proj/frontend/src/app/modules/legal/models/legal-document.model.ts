/**
 * The shape of a legal document.
 *
 * The prose lives in `legal-documents.ts` as structured data rather than as template markup, so
 * that one renderer can present all five documents, the table of contents can be derived from
 * the sections themselves, and a table row count or a heading level is never hand-written twice.
 */

/** The five documents TaskFlow publishes. */
export type LegalDocumentId = 'copyright' | 'terms' | 'privacy' | 'disclaimer' | 'licenses';

/**
 * A piece of body content inside a section. The renderer turns each kind into real semantic
 * markup — a list becomes a `<ul>`, a table becomes a `<table>` with a caption and header cells.
 */
export type LegalBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'list'; items: string[] }
  | { kind: 'note'; text: string }
  | { kind: 'table'; caption: string; head: string[]; rows: string[][] };

/** A numbered section of a document. */
export interface LegalSection {
  /** The anchor id, used as the section element's id and by the table of contents link. */
  id: string;
  /** The section heading, shown in the body and in the table of contents. */
  heading: string;
  /** The body of the section. */
  blocks: LegalBlock[];
}

/** One published document. */
export interface LegalDocument {
  /** The id the route carries in its `data`. */
  id: LegalDocumentId;
  /** The document title, shown as the page heading and in the document head. */
  title: string;
  /** The short label used for the document's link in the footer. */
  navLabel: string;
  /** One line describing the document, shown under the title. */
  summary: string;
  /** The date the document was last updated, for example "21 October 2026". */
  updated: string;
  /** The sections of the document, in the order they are printed. */
  sections: LegalSection[];
}
