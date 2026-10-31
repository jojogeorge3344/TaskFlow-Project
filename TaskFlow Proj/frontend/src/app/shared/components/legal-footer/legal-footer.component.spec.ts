import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { LEGAL_DOCUMENTS } from '../../../modules/legal/utils/legal-documents';
import { legalOwnershipLine } from '../../../modules/legal/utils/legal-owner';
import { LegalFooterComponent } from './legal-footer.component';

describe('LegalFooterComponent', () => {
  let fixture: ComponentFixture<LegalFooterComponent>;

  function render(documentId?: string, variant?: 'surface' | 'dark'): HTMLElement {
    const deepest = { snapshot: { data: documentId ? { documentId } : {} }, firstChild: null };
    TestBed.configureTestingModule({
      imports: [LegalFooterComponent],
      providers: [provideRouter([]), { provide: ActivatedRoute, useValue: { firstChild: deepest } }]
    });
    fixture = TestBed.createComponent(LegalFooterComponent);
    if (variant) {
      fixture.componentRef.setInput('variant', variant);
    }
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the ownership line', () => {
    const footer = render();

    expect(footer.querySelector('.legal-footer__line')?.textContent).toContain(legalOwnershipLine());
  });

  it('links to every legal document under /legal', () => {
    const links = Array.from(render().querySelectorAll<HTMLAnchorElement>('.legal-footer__link'));

    expect(links.map((link) => link.getAttribute('href'))).toEqual(LEGAL_DOCUMENTS.map((doc) => `/legal/${doc.id}`));
    expect(links.map((link) => link.textContent?.trim())).toEqual(LEGAL_DOCUMENTS.map((doc) => doc.navLabel));
  });

  it('marks the document named by the deepest route as the current page', () => {
    const links = Array.from(render('privacy').querySelectorAll<HTMLAnchorElement>('.legal-footer__link'));
    const current = links.filter((link) => link.getAttribute('aria-current') === 'page');

    expect(current.length).toBe(1);
    expect(current[0].getAttribute('href')).toBe('/legal/privacy');
  });

  it('marks no link as current outside the legal pages', () => {
    const links = Array.from(render().querySelectorAll('.legal-footer__link'));

    expect(links.some((link) => link.hasAttribute('aria-current'))).toBeFalse();
  });

  it('uses the surface style by default and the dark style when asked', () => {
    expect(render().querySelector('footer')?.classList).not.toContain('legal-footer--dark');

    TestBed.resetTestingModule();
    expect(render(undefined, 'dark').querySelector('footer')?.classList).toContain('legal-footer--dark');
  });
});
