import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs/operators';
import { getLegalDocument } from '../utils/legal-documents';
import { LEGAL_APPLICATION, LEGAL_OWNER, legalOwnershipLine, resolveLegalTokens } from '../utils/legal-owner';
import { LegalBlock } from '../models/legal-document.model';
import { AuthService } from '../../../shared/auth/auth.service';
import { dashboardRouteFor } from '../../../shared/auth/role-routes';

@Component({
  selector: 'app-legal-page',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './legal-page.component.html',
  styleUrl: './legal-page.component.css'
})
export class LegalPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  private readonly routeData = toSignal(
    this.route.data.pipe(map((data) => data['documentId'] as string | undefined)),
    { initialValue: undefined }
  );

  readonly applicationName = LEGAL_APPLICATION.name;
  readonly applicationTitle = LEGAL_APPLICATION.title;
  readonly ownershipLine = legalOwnershipLine();
  readonly owner = LEGAL_OWNER.name;
  readonly year = LEGAL_OWNER.year;
  readonly email = LEGAL_OWNER.email;

  readonly document = computed(() => {
    const id = this.routeData();
    if (!id) {
      return undefined;
    }
    return getLegalDocument(id);
  });

  readonly resolvedDocument = computed(() => {
    const doc = this.document();
    if (!doc) {
      return undefined;
    }
    // Resolve tokens on all text content so the rendered page uses LEGAL_OWNER.
    const cloned = JSON.parse(JSON.stringify(doc)) as typeof doc;
    for (const section of cloned.sections) {
      section.heading = resolveLegalTokens(section.heading);
      for (const block of section.blocks) {
        if (block.kind === 'paragraph' || block.kind === 'note') {
          block.text = resolveLegalTokens(block.text);
        } else if (block.kind === 'list') {
          block.items = block.items.map((item) => resolveLegalTokens(item));
        } else if (block.kind === 'table') {
          block.caption = resolveLegalTokens(block.caption);
          block.head = block.head.map((h) => resolveLegalTokens(h));
          block.rows = block.rows.map((row) => row.map((cell) => resolveLegalTokens(cell)));
        }
      }
    }
    cloned.title = resolveLegalTokens(cloned.title);
    cloned.summary = resolveLegalTokens(cloned.summary);
    cloned.navLabel = resolveLegalTokens(cloned.navLabel);
    return cloned;
  });

  readonly backLink = computed(() => {
    const user = this.auth.currentUser();
    if (user) {
      // user.role is UserRole (string); pass it straight to dashboardRouteFor,
      // which defaults unknown roles to /me.
      return { path: dashboardRouteFor(user.role), label: 'Back to the application' };
    }
    return { path: '/login', label: 'Back to sign in' };
  });

  trackSection(_: number, section: { id: string }): string {
    return section.id;
  }

  trackBlock(_: number, block: LegalBlock): string {
    return block.kind;
  }

  trackItem(index: number): number {
    return index;
  }
}
