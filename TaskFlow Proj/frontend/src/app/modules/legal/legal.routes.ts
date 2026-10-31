import { Routes } from '@angular/router';

const legalPage = () => import('./pages/legal-page.component').then((m) => m.LegalPageComponent);

/** The public legal documents, one route per document. */
export const LEGAL_ROUTES: Routes = [
  { path: 'legal/copyright', loadComponent: legalPage, data: { documentId: 'copyright' } },
  { path: 'legal/terms', loadComponent: legalPage, data: { documentId: 'terms' } },
  { path: 'legal/privacy', loadComponent: legalPage, data: { documentId: 'privacy' } },
  { path: 'legal/disclaimer', loadComponent: legalPage, data: { documentId: 'disclaimer' } },
  { path: 'legal/licenses', loadComponent: legalPage, data: { documentId: 'licenses' } }
];
