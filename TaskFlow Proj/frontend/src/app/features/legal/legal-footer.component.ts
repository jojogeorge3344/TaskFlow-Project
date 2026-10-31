import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LEGAL_APPLICATION, legalOwnershipLine } from './legal-owner';
import { LEGAL_DOCUMENTS } from './legal-documents';
import { ActivatedRoute } from '@angular/router';
import { LegalFooterVariant } from './legal-footer.model';

@Component({
  selector: 'app-legal-footer',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './legal-footer.component.html',
  styleUrl: './legal-footer.component.css'
})
export class LegalFooterComponent {
  private readonly route = inject(ActivatedRoute);

  readonly variant = input<LegalFooterVariant>('surface');
  readonly applicationName = LEGAL_APPLICATION.name;
  readonly applicationTitle = LEGAL_APPLICATION.title;
  readonly ownershipLine = legalOwnershipLine();
  readonly links = LEGAL_DOCUMENTS.map((doc) => ({
    id: doc.id,
    path: `/legal/${doc.id}`,
    label: doc.navLabel
  }));

  readonly currentDocumentId = computed(() => {
    let current: ActivatedRoute = this.route;
    while (current.firstChild) {
      current = current.firstChild;
    }
    const id = current.snapshot?.data?.['documentId'];
    return typeof id === 'string' ? id : undefined;
  });

  isActive(id: string): boolean {
    return this.currentDocumentId() === id;
  }
}
