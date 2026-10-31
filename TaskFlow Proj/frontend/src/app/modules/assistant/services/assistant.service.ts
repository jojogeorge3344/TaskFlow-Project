import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { apiErrorMessage } from '../../../shared/utils/api-error';
import { AuthService } from '../../../shared/auth/auth.service';
import { AiProviderKind, AiProviderOption, AssistantActionResult, AssistantConversation, AssistantMessage, AssistantPendingAction, AssistantReply, EditMailAction } from '../models/assistant.model';
import { FailedSend } from './assistant.model';

export const TFA_PROVIDER_KEY = 'tfa.provider';
export const TFA_CLOUD_ACK_KEY = 'tfa.cloudAck';

/** How long the list of available providers is trusted before opening the panel asks the server again. */
export const PROVIDER_REFRESH_MS = 60_000;

/** TFA, the in-app assistant: the open panel, the chosen model, the conversation and the actions the user confirms.
 * The conversation and the pending actions live on the server; this service only shows them and sends the user's
 * choices. It never decides whether an action is allowed. */
@Injectable({ providedIn: 'root' })
export class AssistantService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly baseUrl = '/api/v1/assistant';

  readonly panelOpen = signal(false);
  readonly providers = signal<AiProviderOption[]>([]);
  readonly providersLoaded = signal(false);
  readonly messages = signal<AssistantMessage[]>([]);
  readonly conversationLoaded = signal(false);
  readonly sending = signal(false);

  /** A provider problem from the last send. The user's message stays visible so Retry can send it again. */
  readonly failed = signal<FailedSend | null>(null);

  /** A message held until the user acknowledges that a Cloud provider will receive it. */
  readonly heldMessage = signal<string | null>(null);

  private readonly chosenKind = signal<AiProviderKind | null>(readStored(TFA_PROVIDER_KEY) as AiProviderKind | null);

  /** The selected provider: the remembered one when it is still configured, otherwise the first available. */
  readonly selected = computed(() => {
    const options = this.providers();
    return options.find((option) => option.kind === this.chosenKind()) ?? options.at(0) ?? null;
  });

  readonly available = computed(() => this.providers().length > 0);

  /** Providers the user has acknowledged as Cloud providers, as a signal so the panel can show the notice. */
  private readonly acknowledged = signal<AiProviderKind[]>(readAcknowledged());

  /** True when the selected provider is a Cloud one the user has not yet acknowledged. */
  readonly needsCloudNotice = computed(() => {
    const provider = this.selected();
    return provider !== null && provider.locality === 'Cloud' && !this.acknowledged().includes(provider.kind);
  });

  private localId = 0;
  private providersLoadedAt = 0;

  /** The signed-in user's id. The auth service replaces the user object on every token refresh and profile save, so
   * the panel follows the id: a computed on a number only changes when the person really changes. */
  private readonly userId = computed(() => this.auth.currentUser()?.id ?? null);

  constructor() {
    // Whoever signs in gets their own conversation, and nothing of the previous user is left behind.
    effect(() => {
      const userId = this.userId();
      untracked(() => {
        this.resetState();
        if (userId !== null) {
          this.loadProviders();
          this.loadConversation();
        }
      });
    });
  }

  open(): void {
    this.panelOpen.set(true);
    if (this.userId() !== null && Date.now() - this.providersLoadedAt > PROVIDER_REFRESH_MS) {
      this.loadProviders();
    }
  }

  close(): void {
    this.panelOpen.set(false);
  }

  /** On a phone the panel fills the screen, so following a link closes it to show the page. */
  closeOnPhone(): void {
    if (window.matchMedia('(max-width: 767px)').matches) {
      this.panelOpen.set(false);
    }
  }

  toggle(): void {
    this.panelOpen.update((open) => !open);
  }

  loadProviders(): void {
    this.http.get<AiProviderOption[]>(`${this.baseUrl}/providers`).subscribe({
      next: (options) => {
        this.providers.set(options);
        this.providersLoaded.set(true);
        this.providersLoadedAt = Date.now();
      },
      error: () => {
        this.providers.set([]);
        this.providersLoaded.set(true);
      }
    });
  }

  loadConversation(): void {
    this.http.get<AssistantConversation>(`${this.baseUrl}/conversation`).subscribe({
      next: (conversation) => {
        this.messages.set(conversation.messages);
        this.conversationLoaded.set(true);
      },
      error: () => this.conversationLoaded.set(true)
    });
  }

  /** Changes the model for the next message. The conversation stays as it is. */
  selectProvider(kind: AiProviderKind): void {
    this.chosenKind.set(kind);
    writeStored(TFA_PROVIDER_KEY, kind);
  }

  isAcknowledged(kind: AiProviderKind): boolean {
    return this.acknowledged().includes(kind);
  }

  /** Sends the message with the selected model. The first message to a Cloud provider is held until the user
   * acknowledges that the message and related task details are sent to it. */
  send(text: string): void {
    const trimmed = text.trim();
    const provider = this.selected();
    if (!trimmed || !provider || this.sending()) {
      return;
    }

    if (provider.locality === 'Cloud' && !this.isAcknowledged(provider.kind)) {
      this.heldMessage.set(trimmed);
      return;
    }

    this.dispatch(trimmed, provider.kind);
  }

  acknowledgeCloud(): void {
    const provider = this.selected();
    const held = this.heldMessage();
    if (!provider) {
      return;
    }

    this.acknowledged.set([...new Set([...this.acknowledged(), provider.kind])]);
    writeStored(TFA_CLOUD_ACK_KEY, JSON.stringify(this.acknowledged()));
    this.heldMessage.set(null);
    if (held) {
      this.dispatch(held, provider.kind);
    }
  }

  /** Drops a held message and returns its text, so the panel can put it back in the message box. */
  discardHeld(): string | null {
    const held = this.heldMessage();
    this.heldMessage.set(null);
    return held;
  }

  /** Sends the failed message again, with the selected model (the user may have switched in the meantime). */
  retry(): void {
    const failed = this.failed();
    if (!failed) {
      return;
    }

    this.messages.update((messages) => messages.filter((message) => !message.local));
    this.failed.set(null);
    this.send(failed.text);
  }

  newChat(): void {
    this.http.post<void>(`${this.baseUrl}/conversation/new`, {}).subscribe({
      next: () => this.clearConversation(),
      error: (err: unknown) => this.addProblem(apiErrorMessage(err, 'Could not start a new chat.'))
    });
  }

  // ------------------------------------------------------------------ actions

  confirm(action: AssistantPendingAction): void {
    this.http.post<AssistantActionResult>(`${this.baseUrl}/actions/${action.id}/confirm`, {}).subscribe({
      next: (result) => {
        this.updateCard(action.id, (card) => ({ ...card, status: result.status, canEdit: false }));
        if (result.reply) {
          this.messages.update((messages) => [...messages, result.reply!]);
        }
      },
      error: (err: unknown) => this.actionFailed(err, 'Could not confirm that action.')
    });
  }

  cancel(action: AssistantPendingAction): void {
    this.http.post<AssistantPendingAction>(`${this.baseUrl}/actions/${action.id}/cancel`, {}).subscribe({
      next: (card) => this.updateCard(action.id, () => card),
      error: (err: unknown) => this.actionFailed(err, 'Could not cancel that action.')
    });
  }

  /** Saves the user's edits to a prepared mail. Errors are returned to the caller so the editor can show them. */
  edit(action: AssistantPendingAction, edit: EditMailAction, done: (error: string | null) => void): void {
    this.http.put<AssistantPendingAction>(`${this.baseUrl}/actions/${action.id}`, edit).subscribe({
      next: (card) => {
        this.updateCard(action.id, () => card);
        done(null);
      },
      error: (err: unknown) => done(apiErrorMessage(err, 'Could not save your changes.'))
    });
  }

  // ------------------------------------------------------------------ internals

  private dispatch(text: string, provider: AiProviderKind): void {
    this.failed.set(null);
    const local: AssistantMessage = {
      id: --this.localId,
      role: 'User',
      content: text,
      kind: 'Text',
      createdAt: new Date().toISOString(),
      provider: null,
      links: [],
      rankedTasks: [],
      action: null,
      sources: [],
      local: true
    };
    this.messages.update((messages) => [...messages, local]);
    this.sending.set(true);

    this.http.post<AssistantReply>(`${this.baseUrl}/conversation/messages`, { text, provider }).subscribe({
      next: (reply) => {
        this.sending.set(false);
        if (reply.failure) {
          // The server stored nothing; the message stays on screen so it can be retried or sent with another model.
          this.failed.set({ failure: reply.failure, text });
          return;
        }

        // A blocked message (it looked like a password) comes back as a notice only, so the typed text is removed too.
        this.messages.update((messages) => [...messages.filter((message) => message.id !== local.id), ...reply.messages]);
      },
      error: (err: unknown) => {
        this.sending.set(false);
        this.addProblem(apiErrorMessage(err, 'TFA could not answer. Please try again.'));
        // An administrator may have changed the providers since the list was loaded.
        this.loadProviders();
      }
    });
  }

  private addProblem(text: string): void {
    this.messages.update((messages) => [
      ...messages,
      {
        id: --this.localId,
        role: 'Assistant',
        content: text,
        kind: 'Notice',
        createdAt: new Date().toISOString(),
        provider: null,
        links: [],
        rankedTasks: [],
        action: null,
        sources: [],
        local: true,
        problem: true
      }
    ]);
  }

  private actionFailed(err: unknown, fallback: string): void {
    this.addProblem(apiErrorMessage(err, fallback));
    // The card may be out of date (expired, already done): show the server's view.
    this.loadConversation();
  }

  private updateCard(id: string, change: (card: AssistantPendingAction) => AssistantPendingAction): void {
    this.messages.update((messages) =>
      messages.map((message) => (message.action?.id === id ? { ...message, action: change(message.action) } : message))
    );
  }

  private clearConversation(): void {
    this.messages.set([]);
    this.failed.set(null);
    this.heldMessage.set(null);
  }

  private resetState(): void {
    this.clearConversation();
    this.providers.set([]);
    this.providersLoaded.set(false);
    this.providersLoadedAt = 0;
    this.conversationLoaded.set(false);
    this.sending.set(false);
    this.panelOpen.set(false);
  }
}

function readStored(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage can be blocked or full; the choice then lasts only for this visit.
  }
}

function readAcknowledged(): AiProviderKind[] {
  try {
    const parsed: unknown = JSON.parse(readStored(TFA_CLOUD_ACK_KEY) ?? '[]');
    return Array.isArray(parsed) ? (parsed as AiProviderKind[]) : [];
  } catch {
    return [];
  }
}
