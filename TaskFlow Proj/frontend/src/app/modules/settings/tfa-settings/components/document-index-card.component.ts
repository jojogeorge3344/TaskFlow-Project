import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../../../shared/auth/auth.service';
import { Permissions } from '../../../../shared/auth/permissions';
import { IconComponent } from '../../../../shared/components/icon/icon.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { DocumentIndexStatus } from '../services/ai-provider-admin.model';
import { AiProviderAdminService } from '../services/ai-provider-admin.service';

/** How often the card asks for the status while an indexing run is going. */
export const DOCUMENT_STATUS_POLL_MS = 3000;

/** The Document search card on TFA Settings: the documents folder TFA answers from, the embedding model, how many files and
 * chunks are indexed, when indexing last finished and the last error. Seen with settings.ai.view; Re-index now needs
 * settings.ai.edit. While a run is going the card checks the status every 3 seconds. */
@Component({
  selector: 'app-document-index-card',
  standalone: true,
  imports: [DatePipe, IconComponent],
  templateUrl: './document-index-card.component.html',
  styleUrl: './document-index-card.component.css'
})
export class DocumentIndexCardComponent implements OnInit, OnDestroy {
  private readonly admin = inject(AiProviderAdminService);
  private readonly auth = inject(AuthService);
  private poll: ReturnType<typeof setTimeout> | null = null;

  readonly visible = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAiView);
  });

  readonly canReindex = computed(() => {
    this.auth.currentUser();
    return this.auth.hasPermission(Permissions.SettingsAiEdit);
  });

  readonly status = signal<DocumentIndexStatus | null>(null);
  readonly loadError = signal<string | null>(null);
  readonly requestError = signal<string | null>(null);
  readonly requesting = signal(false);

  ngOnInit(): void {
    if (this.visible()) {
      this.load();
    }
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  load(): void {
    this.loadError.set(null);
    this.admin.getDocumentStatus().subscribe({
      next: (status) => this.show(status),
      error: (err: unknown) => {
        this.stopPolling();
        this.loadError.set(apiErrorMessage(err, 'Could not load the document search status.'));
      }
    });
  }

  reindex(): void {
    if (this.requesting() || this.status()?.isRunning) {
      return;
    }

    this.requesting.set(true);
    this.requestError.set(null);
    this.admin.reindexDocuments().subscribe({
      next: (status) => {
        this.requesting.set(false);
        this.show(status);
      },
      error: (err: unknown) => {
        this.requesting.set(false);
        this.requestError.set(apiErrorMessage(err, 'Could not start indexing.'));
      }
    });
  }

  private show(status: DocumentIndexStatus): void {
    this.status.set(status);
    this.stopPolling();
    if (status.isRunning) {
      this.poll = setTimeout(() => {
        this.poll = null;
        this.load();
      }, DOCUMENT_STATUS_POLL_MS);
    }
  }

  private stopPolling(): void {
    if (this.poll !== null) {
      clearTimeout(this.poll);
      this.poll = null;
    }
  }
}
