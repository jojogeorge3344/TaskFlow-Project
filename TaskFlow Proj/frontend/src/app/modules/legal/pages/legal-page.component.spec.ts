import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from '../../../shared/auth/auth.service';
import { getLegalDocument } from '../utils/legal-documents';
import { LEGAL_OWNER, legalOwnershipLine } from '../utils/legal-owner';
import { LegalPageComponent } from './legal-page.component';

describe('LegalPageComponent', () => {
  let routeData: BehaviorSubject<Record<string, unknown>>;
  let currentUser: ReturnType<typeof signal<{ role: string } | null>>;
  let fixture: ComponentFixture<LegalPageComponent>;

  function render(documentId: string | undefined): HTMLElement {
    routeData.next(documentId ? { documentId } : {});
    fixture = TestBed.createComponent(LegalPageComponent);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    routeData = new BehaviorSubject<Record<string, unknown>>({});
    currentUser = signal<{ role: string } | null>(null);

    TestBed.configureTestingModule({
      imports: [LegalPageComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { data: routeData.asObservable() } },
        { provide: AuthService, useValue: { currentUser } }
      ]
    });
  });

  it('renders the document named by the route, with a contents entry per section', () => {
    const page = render('terms');
    const terms = getLegalDocument('terms')!;

    expect(page.querySelector('h1')?.textContent).toContain('Terms of Use');
    expect(page.querySelectorAll('.legal__toc-item').length).toBe(terms.sections.length);
    expect(page.querySelectorAll('section.legal__section').length).toBe(terms.sections.length);
  });

  it('replaces every owner token with the LEGAL_OWNER values', () => {
    const page = render('copyright');
    const text = page.textContent ?? '';

    expect(text).not.toContain('{{');
    expect(text).toContain(LEGAL_OWNER.name);
    expect(text).toContain(legalOwnershipLine());
  });

  it('shows the not-found fallback for an unknown document id', () => {
    const page = render('no-such-document');

    expect(page.querySelector('h1')?.textContent).toContain('Legal document not found');
    expect(page.querySelector('.legal__article')).toBeNull();
  });

  it('shows the not-found fallback when the route carries no document id', () => {
    const page = render(undefined);

    expect(page.querySelector('h1')?.textContent).toContain('Legal document not found');
  });

  it('sends a signed-out visitor back to sign in', () => {
    const page = render('privacy');
    const back = page.querySelector<HTMLAnchorElement>('.legal__back')!;

    expect(back.textContent).toContain('Back to sign in');
    expect(back.getAttribute('href')).toBe('/login');
  });

  it('sends a signed-in user back to their dashboard', () => {
    currentUser.set({ role: 'Manager' });
    const page = render('privacy');
    const back = page.querySelector<HTMLAnchorElement>('.legal__back')!;

    expect(back.textContent).toContain('Back to the application');
    expect(back.getAttribute('href')).toBe('/manager');
  });
});
