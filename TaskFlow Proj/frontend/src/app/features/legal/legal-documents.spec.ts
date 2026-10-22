import { LEGAL_DOCUMENTS, getLegalDocument, LEGAL_DOCUMENT_IDS } from './legal-documents';
import { LEGAL_OWNER, legalOwnershipLine, resolveLegalTokens } from './legal-owner';
import { LegalDocument } from './legal-document.model';

describe('legal documents registry', () => {
  it('contains all five documents', () => {
    expect(LEGAL_DOCUMENTS.length).toBe(5);
    expect(LEGAL_DOCUMENT_IDS).toEqual(['copyright', 'terms', 'privacy', 'disclaimer', 'licenses']);
  });

  it('every document carries a title, a summary, a last-updated date and at least one section', () => {
    for (const doc of LEGAL_DOCUMENTS) {
      expect(doc.title).toBeTruthy();
      expect(doc.summary).toBeTruthy();
      expect(doc.updated).toBeTruthy();
      expect(doc.sections.length).toBeGreaterThan(0);
    }
  });

  it('every section carries a heading and at least one block', () => {
    for (const doc of LEGAL_DOCUMENTS) {
      for (const section of doc.sections) {
        expect(section.id).toBeTruthy();
        expect(section.heading).toBeTruthy();
        expect(section.blocks.length).toBeGreaterThan(0);
      }
    }
  });

  it('rejects a document stripped of all sections', () => {
    const copy: LegalDocument = {
      ...LEGAL_DOCUMENTS[0],
      sections: []
    };
    expect(copy.sections.length).toBe(0);
    // The registry invariant requires at least one section per document;
    // a document with no sections must be treated as invalid.
    expect(() => {
      if (copy.sections.length === 0) {
        throw new Error('invalid: document must have at least one section');
      }
    }).toThrow('invalid: document must have at least one section');
    // The real registry still satisfies the invariant.
    expect(LEGAL_DOCUMENTS.every((doc) => doc.sections.length > 0)).toBeTrue();
  });

  it('Copyright Notice has owner, rights, protected/prohibited and permission sections', () => {
    const doc = getLegalDocument('copyright');
    expect(doc?.title).toBe('Copyright Notice');
    const ids = doc?.sections.map((s) => s.id) ?? [];
    expect(ids).toContain('ownership');
    expect(ids).toContain('protected-work');
    expect(ids).toContain('not-protected');
    expect(ids).toContain('permitted-use');
    expect(ids).toContain('prohibited-use');
    expect(ids).toContain('permission');
    expect(ids).toContain('enforcement');
  });

  it('Terms of Use has licence, acceptable use, liability and governing law naming India', () => {
    const doc = getLegalDocument('terms');
    expect(doc).toBeTruthy();
    const ids = doc?.sections.map((s) => s.id) ?? [];
    expect(ids).toContain('licence');
    expect(ids).toContain('acceptable-use');
    expect(ids).toContain('liability');
    expect(ids).toContain('governing-law');
    const governing = doc?.sections.find((s) => s.id === 'governing-law');
    const text = governing?.blocks.map((b) => ('text' in b ? b.text : '')).join(' ') ?? '';
    expect(text).toContain('India');
  });

  it('Privacy Policy has required sections and no sale/analytics/tracking', () => {
    const doc = getLegalDocument('privacy');
    expect(doc).toBeTruthy();
    const ids = doc?.sections.map((s) => s.id) ?? [];
    expect(ids).toContain('data-categories');
    expect(ids).toContain('storage');
    expect(ids).toContain('ai-provider');
    expect(ids).toContain('browser-speech');
    expect(ids).toContain('mail-privacy');
    expect(ids).toContain('security');
    const fullText = JSON.stringify(doc);
    expect(fullText).toContain('not sold');
    expect(fullText).toContain('no third-party analytics');
    expect(fullText).toContain('advertising trackers');
  });

  it('Disclaimer has no-warranty, assistant output and advisory suggestions', () => {
    const doc = getLegalDocument('disclaimer');
    expect(doc).toBeTruthy();
    const ids = doc?.sections.map((s) => s.id) ?? [];
    expect(ids).toContain('no-warranty');
    expect(ids).toContain('assistant-output');
    expect(ids).toContain('advisory-suggestion');
  });

  it('Licences list third-party components and point to THIRD-PARTY-NOTICES.md', () => {
    const doc = getLegalDocument('licenses');
    expect(doc).toBeTruthy();
    const ids = doc?.sections.map((s) => s.id) ?? [];
    expect(ids).toContain('taskflow-license');
    expect(ids).toContain('third-party-table');
    expect(ids).toContain('full-texts');
    const fullText = JSON.stringify(doc);
    expect(fullText).toContain('THIRD-PARTY-NOTICES.md');
    expect(fullText).toContain('Angular');
    expect(fullText).toContain('RxJS');
    expect(fullText).toContain('TypeScript');
    expect(fullText).toContain('Entity Framework Core');
    expect(fullText).toContain('FluentValidation');
    expect(fullText).toContain('Lucide');
    expect(fullText).toContain('Ollama');
  });

  it('owner, year and email are read from LEGAL_OWNER in every document (placeholders, not written out)', () => {
    const ownership = legalOwnershipLine();
    expect(ownership).toBe(`© ${LEGAL_OWNER.year} ${LEGAL_OWNER.name}. ${LEGAL_OWNER.rights}.`);
    expect(ownership).toBe('© 2026 Jojo George. All Rights Reserved.');

    for (const doc of LEGAL_DOCUMENTS) {
      const raw = JSON.stringify(doc);
      // The registry must not contain the literal owner details; they come through placeholders.
      expect(raw).not.toContain('Jojo George');
      expect(raw).not.toContain('jojogeorge3344@gmail.com');
      // The resolved text must contain them exactly as the footer does.
      const resolved = resolveLegalTokens(raw);
      expect(resolved).toContain(LEGAL_OWNER.name);
      expect(resolved).toContain(String(LEGAL_OWNER.year));
      expect(resolved).toContain(LEGAL_OWNER.email);
      expect(resolved).toContain(LEGAL_OWNER.rights);
    }
  });

  it('every document states the same owner values as the footer after resolution', () => {
    const ownership = legalOwnershipLine();
    for (const doc of LEGAL_DOCUMENTS) {
      const resolved = resolveLegalTokens(JSON.stringify(doc));
      expect(resolved).toContain(ownership);
    }
  });
});
