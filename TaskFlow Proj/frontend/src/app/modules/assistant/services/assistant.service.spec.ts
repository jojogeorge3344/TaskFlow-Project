import { provideHttpClient } from '@angular/common/http';
import { systemPermissions } from '../../../shared/testing/permissions';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthUser } from '../../../shared/auth/auth.models';
import { AuthService } from '../../../shared/auth/auth.service';
import { AiProviderOption, AssistantFailure, AssistantMessage, AssistantPendingAction } from '../models/assistant.model';
import { AssistantService, TFA_CLOUD_ACK_KEY, TFA_PROVIDER_KEY } from './assistant.service';

const ollama: AiProviderOption = { kind: 'Ollama', displayName: 'Ollama', modelName: 'llama3.1:8b', locality: 'Local' };
const gemini: AiProviderOption = { kind: 'Gemini', displayName: 'Google Gemini', modelName: 'gemini-2.0-flash', locality: 'Cloud' };

function message(id: number, role: 'User' | 'Assistant', content: string, extra: Partial<AssistantMessage> = {}): AssistantMessage {
  return {
    id,
    role,
    content,
    kind: 'Text',
    createdAt: '2026-10-19T09:00:00Z',
    provider: role === 'Assistant' ? 'Ollama' : null,
    links: [],
    rankedTasks: [],
    action: null,
    sources: [],
    ...extra
  };
}

function card(id: string, status: AssistantPendingAction['status'] = 'Pending'): AssistantPendingAction {
  return {
    id,
    kind: 'SendMail',
    status,
    expiresAt: '2026-10-19T09:10:00Z',
    title: 'Mail draft',
    fields: [{ label: 'To', value: 'Ravi <ravi@taskflow.com>' }],
    body: 'Hello',
    canEdit: status === 'Pending'
  };
}

describe('AssistantService', () => {
  let service: AssistantService;
  let http: HttpTestingController;
  const user = signal<AuthUser | null>(null);

  function signIn(id = 1): void {
    user.set({ id, fullName: 'Asha', username: 'asha', email: 'asha@taskflow.com', role: 'Employee', employeeCode: null, permissions: systemPermissions('Employee') });
    TestBed.flushEffects();
  }

  function flushStartup(providers: AiProviderOption[] = [ollama, gemini], messages: AssistantMessage[] = []): void {
    http.expectOne('/api/v1/assistant/providers').flush(providers);
    http.expectOne('/api/v1/assistant/conversation').flush({ conversationId: messages.length ? 1 : null, messages });
  }

  beforeEach(() => {
    localStorage.removeItem(TFA_PROVIDER_KEY);
    localStorage.removeItem(TFA_CLOUD_ACK_KEY);
    user.set(null);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: AuthService, useValue: { currentUser: user.asReadonly() } }]
    });
    service = TestBed.inject(AssistantService);
    http = TestBed.inject(HttpTestingController);
    TestBed.flushEffects();
  });

  afterEach(() => {
    http.verify();
    localStorage.removeItem(TFA_PROVIDER_KEY);
    localStorage.removeItem(TFA_CLOUD_ACK_KEY);
  });

  it('loads the providers and the conversation when a user signs in', () => {
    signIn();
    flushStartup([ollama, gemini], [message(1, 'User', 'hi'), message(2, 'Assistant', 'hello')]);

    expect(service.providers().map((p) => p.kind)).toEqual(['Ollama', 'Gemini']);
    expect(service.messages().map((m) => m.content)).toEqual(['hi', 'hello']);
    expect(service.selected()?.kind).toBe('Ollama');
  });

  it('keeps the open panel and the conversation when the same user object is replaced, as a token refresh does', () => {
    signIn(1);
    flushStartup([ollama], [message(1, 'User', 'question'), message(2, 'Assistant', 'answer')]);
    service.open();

    user.set({ id: 1, fullName: 'Asha', username: 'asha', email: 'asha@taskflow.com', role: 'Employee', employeeCode: null, permissions: systemPermissions('Employee') });
    TestBed.flushEffects();
    user.set({ id: 1, fullName: 'Asha Nair', username: 'asha', email: 'asha@taskflow.com', role: 'Employee', employeeCode: null, permissions: systemPermissions('Employee') });
    TestBed.flushEffects();

    expect(service.panelOpen()).toBeTrue();
    expect(service.messages().map((m) => m.content)).toEqual(['question', 'answer']);
    expect(service.providers().length).toBe(1);
    // afterEach verifies that no providers or conversation request was made again.
  });

  it('clears everything of the previous user when someone else signs in or the user signs out', () => {
    signIn(1);
    flushStartup([ollama], [message(1, 'User', 'private question')]);
    service.open();

    user.set(null);
    TestBed.flushEffects();

    expect(service.messages()).toEqual([]);
    expect(service.providers()).toEqual([]);
    expect(service.panelOpen()).toBeFalse();

    signIn(2);
    http.expectOne('/api/v1/assistant/providers').flush([ollama]);
    http.expectOne('/api/v1/assistant/conversation').flush({ conversationId: null, messages: [] });
    expect(service.messages()).toEqual([]);
  });

  it('sends a message with the selected provider and replaces the local copy with the stored messages', () => {
    signIn();
    flushStartup();

    service.send('  what is due this week?  ');

    expect(service.sending()).toBeTrue();
    expect(service.messages()[0]).toEqual(jasmine.objectContaining({ content: 'what is due this week?', local: true }));
    const request = http.expectOne('/api/v1/assistant/conversation/messages');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ text: 'what is due this week?', provider: 'Ollama' });
    request.flush({
      messages: [message(10, 'User', 'what is due this week?'), message(11, 'Assistant', 'Two tasks.')],
      failure: null
    });

    expect(service.sending()).toBeFalse();
    expect(service.messages().map((m) => m.id)).toEqual([10, 11]);
  });

  it('switches model mid-conversation without losing the conversation and remembers the choice', () => {
    signIn();
    flushStartup([ollama, gemini], [message(1, 'User', 'earlier'), message(2, 'Assistant', 'answer')]);
    service.selectProvider('Gemini');
    service.acknowledgeCloud();
    service.send('next question');

    const request = http.expectOne('/api/v1/assistant/conversation/messages');
    expect(request.request.body).toEqual({ text: 'next question', provider: 'Gemini' });
    request.flush({ messages: [message(3, 'User', 'next question'), message(4, 'Assistant', 'ok', { provider: 'Gemini' })], failure: null });

    expect(service.messages().map((m) => m.content)).toEqual(['earlier', 'answer', 'next question', 'ok']);
    expect(localStorage.getItem(TFA_PROVIDER_KEY)).toBe('Gemini');
  });

  it('falls back to the first available provider when the remembered one is no longer configured', () => {
    localStorage.setItem(TFA_PROVIDER_KEY, 'OpenAI');
    signIn();
    flushStartup([ollama]);

    expect(service.selected()?.kind).toBe('Ollama');
  });

  describe('keeping the provider list fresh', () => {
    beforeEach(() => jasmine.clock().install());
    afterEach(() => jasmine.clock().uninstall());

    it('does not ask again when the panel is opened soon after loading, but does after a minute', () => {
      jasmine.clock().mockDate(new Date('2026-10-19T09:00:00Z'));
      signIn();
      flushStartup([ollama, gemini]);

      service.open();
      service.close();
      http.expectNone('/api/v1/assistant/providers');

      jasmine.clock().mockDate(new Date('2026-10-19T09:01:01Z'));
      service.open();
      http.expectOne('/api/v1/assistant/providers').flush([ollama]);

      expect(service.providers().map((p) => p.kind)).toEqual(['Ollama']);
    });

    it('drops a provider an administrator has since disabled from the selector', () => {
      jasmine.clock().mockDate(new Date('2026-10-19T09:00:00Z'));
      signIn();
      flushStartup([ollama, gemini]);
      service.selectProvider('Gemini');
      expect(service.selected()?.kind).toBe('Gemini');

      jasmine.clock().mockDate(new Date('2026-10-19T09:05:00Z'));
      service.open();
      http.expectOne('/api/v1/assistant/providers').flush([ollama]);

      expect(service.selected()?.kind).toBe('Ollama');
    });

    it('reloads the list after a send is refused, for example because the provider is no longer configured', () => {
      signIn();
      flushStartup();
      service.selectProvider('Gemini');
      service.acknowledgeCloud();

      service.send('hello');
      http
        .expectOne('/api/v1/assistant/conversation/messages')
        .flush({ detail: 'Google Gemini is not configured. Choose another model, or ask an administrator to set it up.' }, { status: 400, statusText: 'Bad Request' });
      http.expectOne('/api/v1/assistant/providers').flush([ollama]);

      expect(service.selected()?.kind).toBe('Ollama');
      expect(service.messages().at(-1)?.content).toContain('not configured');
    });
  });

  it('reports no providers as not available', () => {
    signIn();
    flushStartup([]);

    expect(service.available()).toBeFalse();
    service.send('hello');
    http.expectNone('/api/v1/assistant/conversation/messages');
  });

  it('holds the first message to a Cloud provider until the user acknowledges it, then asks once', () => {
    signIn();
    flushStartup();
    service.selectProvider('Gemini');

    service.send('summarise my tasks');
    http.expectNone('/api/v1/assistant/conversation/messages');
    expect(service.heldMessage()).toBe('summarise my tasks');
    expect(service.messages()).toEqual([]);

    service.acknowledgeCloud();
    const first = http.expectOne('/api/v1/assistant/conversation/messages');
    expect(first.request.body.provider).toBe('Gemini');
    first.flush({ messages: [message(1, 'User', 'summarise my tasks'), message(2, 'Assistant', 'done', { provider: 'Gemini' })], failure: null });
    expect(service.heldMessage()).toBeNull();
    expect(service.isAcknowledged('Gemini')).toBeTrue();

    service.send('and again');
    expect(service.heldMessage()).toBeNull();
    http.expectOne('/api/v1/assistant/conversation/messages').flush({ messages: [], failure: null });
  });

  it('never holds a message for a Local provider', () => {
    signIn();
    flushStartup();

    service.send('hello');

    expect(service.heldMessage()).toBeNull();
    http.expectOne('/api/v1/assistant/conversation/messages').flush({ messages: [], failure: null });
  });

  it('lets the user take a held message back', () => {
    signIn();
    flushStartup();
    service.selectProvider('Gemini');
    service.send('hold me');

    expect(service.discardHeld()).toBe('hold me');
    expect(service.heldMessage()).toBeNull();
  });

  it('keeps the message on screen after a provider failure and retries it with whichever model is selected', () => {
    signIn();
    flushStartup();
    const failure: AssistantFailure = {
      kind: 'RateLimited',
      provider: 'Ollama',
      message: 'Ollama is busy.',
      retryAfterSeconds: 30,
      alternatives: [gemini]
    };

    service.send('hello');
    http.expectOne('/api/v1/assistant/conversation/messages').flush({ messages: [], failure });

    expect(service.failed()?.failure.kind).toBe('RateLimited');
    expect(service.failed()?.text).toBe('hello');
    expect(service.messages().map((m) => m.content)).toEqual(['hello']);

    service.selectProvider('Gemini');
    service.acknowledgeCloud();
    service.retry();

    const retry = http.expectOne('/api/v1/assistant/conversation/messages');
    expect(retry.request.body).toEqual({ text: 'hello', provider: 'Gemini' });
    expect(service.failed()).toBeNull();
    expect(service.messages().filter((m) => m.content === 'hello').length).toBe(1);
    retry.flush({ messages: [message(1, 'User', 'hello'), message(2, 'Assistant', 'hi')], failure: null });
  });

  it('removes a blocked message from the screen and shows only the notice', () => {
    signIn();
    flushStartup();

    service.send('my password is Hunter2!');
    http.expectOne('/api/v1/assistant/conversation/messages').flush({
      messages: [message(0, 'Assistant', 'Please never type passwords into chat.', { kind: 'Notice', provider: null })],
      failure: null
    });

    expect(service.messages().map((m) => m.content)).toEqual(['Please never type passwords into chat.']);
    expect(service.messages().some((m) => m.content.includes('Hunter2'))).toBeFalse();
  });

  it('shows a request error (429 or 400) as a message in the chat', () => {
    signIn();
    flushStartup();

    service.send('too fast');
    http
      .expectOne('/api/v1/assistant/conversation/messages')
      .flush({ detail: 'You are sending messages too quickly.' }, { status: 429, statusText: 'Too Many Requests' });
    // A refused send also refreshes the provider list, in case it changed since it was loaded.
    http.expectOne('/api/v1/assistant/providers').flush([ollama, gemini]);

    expect(service.sending()).toBeFalse();
    const last = service.messages().at(-1)!;
    expect(last.content).toBe('You are sending messages too quickly.');
    expect(last.problem).toBeTrue();
  });

  it('starts a new chat and clears the screen', () => {
    signIn();
    flushStartup([ollama], [message(1, 'User', 'old'), message(2, 'Assistant', 'older')]);

    service.newChat();
    const request = http.expectOne('/api/v1/assistant/conversation/new');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.messages()).toEqual([]);
  });

  describe('actions', () => {
    const pending = card('a-1');

    function withCard(): void {
      signIn();
      flushStartup([ollama], [message(1, 'User', 'mail my manager'), message(2, 'Assistant', 'Prepared.', { action: pending })]);
    }

    it('confirms through the confirm endpoint only, updates the card and adds the result message', () => {
      withCard();

      service.confirm(pending);

      const request = http.expectOne('/api/v1/assistant/actions/a-1/confirm');
      expect(request.request.method).toBe('POST');
      request.flush({
        id: 'a-1',
        status: 'Confirmed',
        message: 'Done. Your mail was sent.',
        reply: message(3, 'Assistant', 'Done. Your mail was sent.', { provider: null })
      });

      expect(service.messages()[1].action?.status).toBe('Confirmed');
      expect(service.messages()[1].action?.canEdit).toBeFalse();
      expect(service.messages().at(-1)?.content).toBe('Done. Your mail was sent.');
    });

    it('shows a refusal result and leaves the card refused', () => {
      withCard();

      service.confirm(pending);
      http.expectOne('/api/v1/assistant/actions/a-1/confirm').flush({
        id: 'a-1',
        status: 'Refused',
        message: 'You do not have the required permission to perform this action.',
        reply: message(3, 'Assistant', 'You do not have the required permission to perform this action.', { kind: 'Refusal', provider: null })
      });

      expect(service.messages()[1].action?.status).toBe('Refused');
      expect(service.messages().at(-1)?.kind).toBe('Refusal');
    });

    it('reports an expired or already-done action and refreshes from the server', () => {
      withCard();

      service.confirm(pending);
      http
        .expectOne('/api/v1/assistant/actions/a-1/confirm')
        .flush({ detail: 'This request has expired. Please ask TFA again.' }, { status: 409, statusText: 'Conflict' });
      http.expectOne('/api/v1/assistant/conversation').flush({
        conversationId: 1,
        messages: [message(2, 'Assistant', 'Prepared.', { action: card('a-1', 'Expired') })]
      });

      expect(service.messages()[0].action?.status).toBe('Expired');
    });

    it('cancels and updates the card from the server response', () => {
      withCard();

      service.cancel(pending);
      http.expectOne('/api/v1/assistant/actions/a-1/cancel').flush(card('a-1', 'Cancelled'));

      expect(service.messages()[1].action?.status).toBe('Cancelled');
    });

    it('saves edits before confirming and reports a server error to the editor', () => {
      withCard();
      let first: string | null | undefined;
      let second: string | null | undefined;

      service.edit(pending, { subject: 'New subject', body: 'New body' }, (error) => (first = error));
      const ok = http.expectOne('/api/v1/assistant/actions/a-1');
      expect(ok.request.method).toBe('PUT');
      expect(ok.request.body).toEqual({ subject: 'New subject', body: 'New body' });
      ok.flush({ ...pending, body: 'New body' });
      expect(first).toBeNull();
      expect(service.messages()[1].action?.body).toBe('New body');

      service.edit(pending, { to: ['nobody@elsewhere.example'] }, (error) => (second = error));
      http
        .expectOne('/api/v1/assistant/actions/a-1')
        .flush({ detail: 'These are not active TaskFlow users: nobody@elsewhere.example.' }, { status: 400, statusText: 'Bad Request' });
      expect(second).toContain('nobody@elsewhere.example');
    });
  });
});
