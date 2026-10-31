import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { AuthUser } from '../../../../shared/auth/auth.models';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { fakeAuthService, systemPermissions } from '../../../../shared/testing/permissions';
import { DocumentIndexStatus } from '../services/ai-provider-admin.model';
import { DOCUMENT_STATUS_POLL_MS, DocumentIndexCardComponent } from './document-index-card.component';

const STATUS_URL = '/api/v1/assistant/documents/status';
const REINDEX_URL = '/api/v1/assistant/documents/reindex';

function status(extra: Partial<DocumentIndexStatus> = {}): DocumentIndexStatus {
  return {
    enabled: true,
    sourcePath: 'C:\\TaskFlow Project\\TF - Documents',
    folderFound: true,
    embeddingModel: 'nomic-embed-text',
    fileCount: 89,
    chunkCount: 3412,
    lastIndexedAt: '2026-10-31T09:15:00Z',
    isRunning: false,
    lastError: null,
    lastErrorFile: null,
    ...extra
  };
}

describe('DocumentIndexCardComponent', () => {
  const user = signal<AuthUser | null>(null);
  let fixture: ComponentFixture<DocumentIndexCardComponent>;
  let http: HttpTestingController;

  function create(permissions: string[]): HTMLElement {
    user.set({ id: 1, fullName: 'Admin', username: 'admin', email: 'admin@taskflow.com', role: 'Administrator', employeeCode: null, permissions });
    TestBed.configureTestingModule({
      imports: [DocumentIndexCardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: fakeAuthService(user.asReadonly()) }]
    });
    fixture = TestBed.createComponent(DocumentIndexCardComponent);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  function text(element: HTMLElement, selector: string): string {
    return element.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  afterEach(() => http.verify());

  it('shows the folder, model, file and chunk counts and the last indexed time when idle', () => {
    const element = create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush(status());
    fixture.detectChanges();

    expect(text(element, '[data-field="source-path"]')).toBe('C:\\TaskFlow Project\\TF - Documents');
    expect(text(element, '[data-field="embedding-model"]')).toBe('nomic-embed-text');
    expect(text(element, '[data-field="file-count"]')).toBe('89');
    expect(text(element, '[data-field="chunk-count"]')).toBe('3412');
    expect(text(element, '[data-field="last-indexed"]')).toContain('31 Oct 2026');
    expect(element.querySelector('[data-state="idle"]')).not.toBeNull();
    expect(element.querySelector('[data-field="folder-missing"]')).toBeNull();
    expect(element.querySelector('[data-error="document-index"]')).toBeNull();
    expect((element.querySelector('[data-action="reindex-documents"]') as HTMLButtonElement).disabled).toBeFalse();
  });

  it('shows "Not yet" before the first run', () => {
    const element = create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush(status({ lastIndexedAt: null, fileCount: 0, chunkCount: 0 }));
    fixture.detectChanges();

    expect(text(element, '[data-field="last-indexed"]')).toBe('Not yet');
  });

  it('shows a run as running, disables the button and polls every 3 seconds until it finishes', fakeAsync(() => {
    const element = create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush(status());
    fixture.detectChanges();

    (element.querySelector('[data-action="reindex-documents"]') as HTMLButtonElement).click();
    const request = http.expectOne(REINDEX_URL);
    expect(request.request.method).toBe('POST');
    request.flush(status({ isRunning: true }));
    fixture.detectChanges();

    expect(element.querySelector('[data-state="running"]')).not.toBeNull();
    expect((element.querySelector('[data-action="reindex-documents"]') as HTMLButtonElement).disabled).toBeTrue();

    tick(DOCUMENT_STATUS_POLL_MS - 1);
    http.expectNone(STATUS_URL);
    tick(1);
    http.expectOne(STATUS_URL).flush(status({ isRunning: true, fileCount: 40 }));
    fixture.detectChanges();
    expect(text(element, '[data-field="file-count"]')).toBe('40');

    tick(DOCUMENT_STATUS_POLL_MS);
    http.expectOne(STATUS_URL).flush(status({ fileCount: 90 }));
    fixture.detectChanges();

    expect(element.querySelector('[data-state="idle"]')).not.toBeNull();
    expect(text(element, '[data-field="file-count"]')).toBe('90');
    tick(DOCUMENT_STATUS_POLL_MS * 2);
    http.expectNone(STATUS_URL);
    discardPeriodicTasks();
  }));

  it('starts polling when the card opens on a run that is already going', fakeAsync(() => {
    create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush(status({ isRunning: true }));

    tick(DOCUMENT_STATUS_POLL_MS);
    http.expectOne(STATUS_URL).flush(status());
    fixture.destroy();
  }));

  it('shows the last error with its file, and a missing folder', () => {
    const element = create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush(
      status({
        folderFound: false,
        lastError: "The embedding model 'nomic-embed-text' is not installed in Ollama. Run: ollama pull nomic-embed-text",
        lastErrorFile: 'TF - FRD/FRD-Task-Management.md'
      })
    );
    fixture.detectChanges();

    expect(text(element, '[data-error="document-index"]')).toContain('ollama pull nomic-embed-text');
    expect(text(element, '[data-field="error-file"]')).toBe('File: TF - FRD/FRD-Task-Management.md');
    expect(text(element, '[data-field="folder-missing"]')).toBe('Not found');
  });

  it('shows a load error with a retry', () => {
    const element = create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush({ detail: 'Server error' }, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();

    expect(element.querySelector('[data-error="document-status"]')).not.toBeNull();
    (element.querySelector('[data-action="retry-document-status"]') as HTMLButtonElement).click();
    http.expectOne(STATUS_URL).flush(status());
    fixture.detectChanges();
    expect(element.querySelector('[data-error="document-status"]')).toBeNull();
  });

  it('hides Re-index now from someone who can view but not edit TFA settings', () => {
    const element = create([Permissions.SettingsAiView]);
    http.expectOne(STATUS_URL).flush(status());
    fixture.detectChanges();

    expect(element.querySelector('[data-section="document-search"]')).not.toBeNull();
    expect(element.querySelector('[data-action="reindex-documents"]')).toBeNull();
  });

  it('shows nothing and asks for nothing without settings.ai.view', () => {
    const element = create(systemPermissions('Manager'));

    expect(element.querySelector('[data-section="document-search"]')).toBeNull();
    http.expectNone(STATUS_URL);
  });

  it('says when document search is switched off and offers no re-index', () => {
    const element = create(systemPermissions('Administrator'));
    http.expectOne(STATUS_URL).flush(status({ enabled: false, fileCount: 0, chunkCount: 0, lastIndexedAt: null }));
    fixture.detectChanges();

    expect(element.querySelector('[data-state="disabled"]')).not.toBeNull();
    expect(text(element, '[data-field="disabled-note"]')).toContain('DocumentKnowledge:Enabled');
    expect(element.querySelector('[data-action="reindex-documents"]')).toBeNull();
  });
});
