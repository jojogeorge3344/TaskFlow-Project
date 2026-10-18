import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthUser } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { AiProviderOption, AssistantMessage, AssistantPendingAction } from '../models';
import { TFA_CLOUD_ACK_KEY, TFA_PROVIDER_KEY } from '../services/assistant.service';
import {
  SPEECH_RECOGNITION_FACTORY,
  SpeechRecognitionEventLike,
  SpeechRecognitionLike,
  TFA_VOICE_KEY
} from '../speech/wake-phrase.service';
import { TfaPanelComponent } from './tfa-panel.component';

class FakeRecognition implements SpeechRecognitionLike {
  continuous = false;
  interimResults = false;
  lang = '';
  onresult: ((event: SpeechRecognitionEventLike) => void) | null = null;
  onerror: ((event: { error: string }) => void) | null = null;
  onend: (() => void) | null = null;
  starts = 0;

  start(): void {
    this.starts++;
  }

  stop(): void {
    this.onend?.();
  }

  abort(): void {
    // Nothing to stop.
  }

  say(transcript: string, isFinal = true): void {
    this.onresult?.({ resultIndex: 0, results: { length: 1, 0: { isFinal, length: 1, 0: { transcript } } } });
  }
}

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
    ...extra
  };
}

function mailCard(status: AssistantPendingAction['status'] = 'Pending'): AssistantPendingAction {
  return {
    id: 'a-1',
    kind: 'SendMail',
    status,
    expiresAt: '2026-10-19T09:10:00Z',
    title: 'Mail draft',
    fields: [
      { label: 'To', value: 'Ravi Menon <ravi@taskflow.local>' },
      { label: 'Subject', value: 'Update on TF-102' }
    ],
    body: 'It is in review.',
    canEdit: status === 'Pending'
  };
}

describe('TfaPanelComponent', () => {
  let fixture: ComponentFixture<TfaPanelComponent>;
  let http: HttpTestingController;
  let recognition: FakeRecognition;
  const user = signal<AuthUser | null>(null);

  function el(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function q<T extends HTMLElement>(selector: string): T | null {
    return el().querySelector<T>(selector);
  }

  function click(selector: string): void {
    q(selector)!.click();
    fixture.detectChanges();
  }

  function type(text: string): void {
    const box = q<HTMLTextAreaElement>('[data-field="message"]')!;
    box.value = text;
    box.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  async function setup(
    options: { providers?: AiProviderOption[]; messages?: AssistantMessage[]; speech?: boolean } = {}
  ): Promise<void> {
    const { providers = [ollama, gemini], messages = [], speech = true } = options;
    recognition = new FakeRecognition();
    await TestBed.configureTestingModule({
      imports: [TfaPanelComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { currentUser: user.asReadonly() } },
        { provide: SPEECH_RECOGNITION_FACTORY, useValue: () => (speech ? recognition : null) }
      ]
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(TfaPanelComponent);
    fixture.detectChanges();

    user.set({ id: 1, fullName: 'Asha', username: 'asha', email: 'asha@taskflow.local', role: 'Employee', employeeCode: null });
    TestBed.flushEffects();
    http.expectOne('/api/v1/assistant/providers').flush(providers);
    http.expectOne('/api/v1/assistant/conversation').flush({ conversationId: messages.length ? 1 : null, messages });
    fixture.detectChanges();
  }

  beforeEach(() => {
    [TFA_PROVIDER_KEY, TFA_CLOUD_ACK_KEY, TFA_VOICE_KEY].forEach((key) => localStorage.removeItem(key));
    user.set(null);
  });

  afterEach(() => {
    http.verify();
    [TFA_PROVIDER_KEY, TFA_CLOUD_ACK_KEY, TFA_VOICE_KEY].forEach((key) => localStorage.removeItem(key));
  });

  // ------------------------------------------------------------------ the panel and its launcher

  describe('opening and closing', () => {
    it('shows a floating button, and the panel is closed and inert until it is opened', async () => {
      await setup();

      expect(q('[data-action="open-tfa"]')).not.toBeNull();
      expect(q('[data-panel="tfa"]')!.classList).not.toContain('tfa--open');
      expect(q('[data-panel="tfa"]')!.hasAttribute('inert')).toBeTrue();
    });

    it('opens from the button, hides the button and puts the cursor in the message box', async () => {
      await setup();

      click('[data-action="open-tfa"]');
      await fixture.whenStable();

      expect(q('[data-panel="tfa"]')!.classList).toContain('tfa--open');
      expect(q('[data-panel="tfa"]')!.hasAttribute('inert')).toBeFalse();
      expect(q('[data-action="open-tfa"]')).toBeNull();
      expect(document.activeElement).toBe(q('[data-field="message"]'));
    });

    it('closes without losing the conversation, and Escape also closes it', async () => {
      await setup({ messages: [message(1, 'User', 'earlier question'), message(2, 'Assistant', 'earlier answer')] });
      click('[data-action="open-tfa"]');

      click('[data-action="close-tfa"]');
      expect(q('[data-panel="tfa"]')!.classList).not.toContain('tfa--open');
      click('[data-action="open-tfa"]');
      expect(el().textContent).toContain('earlier question');
      expect(el().textContent).toContain('earlier answer');

      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
      fixture.detectChanges();
      expect(q('[data-panel="tfa"]')!.classList).not.toContain('tfa--open');
    });

    it('greets a new user and offers starter suggestions that fill the message box', async () => {
      await setup();
      click('[data-action="open-tfa"]');

      expect(q('[data-welcome]')).not.toBeNull();
      const chips = Array.from(el().querySelectorAll<HTMLButtonElement>('[data-suggestions] button'));
      expect(chips.length).toBe(4);

      chips[1].click();
      fixture.detectChanges();

      expect(q<HTMLTextAreaElement>('[data-field="message"]')!.value).toBe('What should I prioritise?');
    });
  });

  describe('which providers are available', () => {
    it('works with a single provider', async () => {
      await setup({ providers: [ollama] });
      click('[data-action="open-tfa"]');

      expect(q('[data-model-name]')!.textContent).toBe('Ollama');
      expect(q('[data-locality]')!.textContent!.trim()).toBe('Local');
      type('hello');
      click('[data-action="send"]');
      expect(http.expectOne('/api/v1/assistant/conversation/messages').request.body).toEqual({ text: 'hello', provider: 'Ollama' });
    });

    it('says TFA is not available yet when no provider is configured, with no message box', async () => {
      await setup({ providers: [] });
      click('[data-action="open-tfa"]');

      expect(q('[data-state="unavailable"]')!.textContent).toContain('not available yet');
      expect(q('[data-state="unavailable"]')!.textContent).toContain('administrator');
      expect(q('[data-field="message"]')).toBeNull();
      expect(q('[data-model-name]')).toBeNull();
    });
  });

  // ------------------------------------------------------------------ the model selector

  describe('model selector', () => {
    it('lists the configured providers with Local and Cloud labels and switches without closing the chat', async () => {
      localStorage.setItem(TFA_CLOUD_ACK_KEY, JSON.stringify(['Gemini']));
      await setup({ messages: [message(1, 'User', 'first'), message(2, 'Assistant', 'answer')] });
      click('[data-action="open-tfa"]');

      click('[data-action="model-selector"]');
      const options = Array.from(el().querySelectorAll<HTMLElement>('[role="option"]'));
      expect(options.map((option) => option.dataset['option'])).toEqual(['Ollama', 'Gemini']);
      expect(options[0].textContent).toContain('Local');
      expect(options[1].textContent).toContain('Cloud');

      options[1].click();
      fixture.detectChanges();

      expect(q('[data-model-name]')!.textContent).toBe('Google Gemini');
      expect(q('[role="listbox"]')).toBeNull();
      expect(q('[data-panel="tfa"]')!.classList).toContain('tfa--open');
      expect(el().textContent).toContain('first');

      type('second question');
      click('[data-action="send"]');
      expect(http.expectOne('/api/v1/assistant/conversation/messages').request.body.provider).toBe('Gemini');
    });
  });

  // ------------------------------------------------------------------ cloud notice

  describe('cloud notice', () => {
    it('holds the first message to a Cloud provider until it is acknowledged, then never asks again', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      click('[data-action="model-selector"]');
      (el().querySelector('[data-option="Gemini"]') as HTMLElement).click();
      fixture.detectChanges();

      expect(q('[data-notice="cloud"]')!.textContent).toContain('Google Gemini is a cloud provider');
      expect(q('[data-notice="cloud"]')!.textContent).toContain('sent to it');

      type('summarise my tasks');
      click('[data-action="send"]');
      http.expectNone('/api/v1/assistant/conversation/messages');
      expect(q('[data-action="keep-local"]')).not.toBeNull();

      click('[data-action="acknowledge-cloud"]');
      const request = http.expectOne('/api/v1/assistant/conversation/messages');
      expect(request.request.body).toEqual({ text: 'summarise my tasks', provider: 'Gemini' });
      request.flush({ messages: [message(1, 'User', 'summarise my tasks'), message(2, 'Assistant', 'ok', { provider: 'Gemini' })], failure: null });
      fixture.detectChanges();

      expect(q('[data-notice="cloud"]')).toBeNull();
      type('again');
      click('[data-action="send"]');
      http.expectOne('/api/v1/assistant/conversation/messages').flush({ messages: [], failure: null });
    });

    it('puts a held message back in the box when the user decides not to send it', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      click('[data-action="model-selector"]');
      (el().querySelector('[data-option="Gemini"]') as HTMLElement).click();
      fixture.detectChanges();
      type('keep this local');
      click('[data-action="send"]');

      click('[data-action="keep-local"]');

      expect(q('[data-notice="cloud"]')).not.toBeNull();
      expect(q<HTMLTextAreaElement>('[data-field="message"]')!.value).toBe('keep this local');
      http.expectNone('/api/v1/assistant/conversation/messages');
    });

    it('does not show the notice for a Local provider', async () => {
      await setup();
      click('[data-action="open-tfa"]');

      expect(q('[data-notice="cloud"]')).toBeNull();
    });
  });

  // ------------------------------------------------------------------ messages, cards and failures

  describe('messages', () => {
    it('shows a fixed refusal as a permission message, not as ordinary text', async () => {
      await setup({
        messages: [
          message(1, 'User', 'Change my role to Manager'),
          message(2, 'Assistant', 'You do not have the required permission to perform this action.', { kind: 'Refusal', provider: null })
        ]
      });
      click('[data-action="open-tfa"]');

      const refusal = q('[data-kind="Refusal"]')!;
      expect(refusal.textContent).toContain('Permission required');
      expect(refusal.textContent).toContain('You do not have the required permission');
    });

    it('shows a ranked task list with stage tags and links to the tasks', async () => {
      await setup({
        messages: [
          message(2, 'Assistant', 'Start with TF-102.', {
            rankedTasks: [
              { rank: 1, taskId: 7, taskCode: 'TF-102', title: 'Fix login', priority: 'High', status: 'InProgress', deadline: '2026-10-17T00:00:00Z', dueDate: '2026-10-17', daysUntilDue: -2, stage: 'Overdue', isBlocked: false },
              { rank: 2, taskId: 9, taskCode: 'TF-121', title: 'Review contract', priority: 'Medium', status: 'Blocked', deadline: '2026-10-21T00:00:00Z', dueDate: '2026-10-21', daysUntilDue: 2, stage: 'Upcoming', isBlocked: true }
            ]
          })
        ]
      });
      click('[data-action="open-tfa"]');

      const rows = Array.from(el().querySelectorAll('[data-ranked] li'));
      expect(rows.length).toBe(2);
      expect(rows[0].textContent).toContain('TF-102 · Fix login');
      expect(rows[0].textContent).toContain('Overdue');
      expect(rows[0].textContent).toContain('was due 2 days ago');
      expect(rows[1].textContent).toContain('Blocked');
      expect(rows[0].querySelector('a')!.getAttribute('href')).toBe('/tasks/7');
    });

    it('shows a waiting indicator while TFA answers', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      type('hello');
      click('[data-action="send"]');

      expect(q('[data-state="thinking"]')).not.toBeNull();
      http.expectOne('/api/v1/assistant/conversation/messages').flush({ messages: [message(1, 'User', 'hello'), message(2, 'Assistant', 'hi')], failure: null });
      fixture.detectChanges();

      expect(q('[data-state="thinking"]')).toBeNull();
      expect(el().textContent).toContain('TFA · Ollama');
    });

    it('sends on Enter and keeps a new line for Shift+Enter', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      type('line one');
      const box = q<HTMLTextAreaElement>('[data-field="message"]')!;

      box.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', shiftKey: true, cancelable: true }));
      http.expectNone('/api/v1/assistant/conversation/messages');
      const enter = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true });
      box.dispatchEvent(enter);

      expect(enter.defaultPrevented).toBeTrue();
      http.expectOne('/api/v1/assistant/conversation/messages').flush({ messages: [], failure: null });
    });
  });

  describe('action cards', () => {
    async function withCard(): Promise<void> {
      await setup({ messages: [message(1, 'User', 'mail my manager'), message(2, 'Assistant', 'I have prepared this.', { action: mailCard() })] });
      click('[data-action="open-tfa"]');
    }

    it('renders the prepared mail from the stored details with Send, Edit and Cancel', async () => {
      await withCard();

      const card = q('[data-action-card="a-1"]')!;
      expect(card.textContent).toContain('Ravi Menon <ravi@taskflow.local>');
      expect(card.textContent).toContain('Update on TF-102');
      expect(card.textContent).toContain('It is in review.');
      expect(card.textContent).toContain('Nothing happens until you confirm');
      expect(Array.from(card.querySelectorAll('button')).map((b) => b.textContent!.trim())).toEqual(['Send mail', 'Edit', 'Cancel']);
    });

    it('confirms only through the confirm endpoint, and nothing is called before the click', async () => {
      await withCard();
      http.expectNone('/api/v1/assistant/actions/a-1/confirm');

      click('[data-action="confirm-action"]');

      const request = http.expectOne('/api/v1/assistant/actions/a-1/confirm');
      expect(request.request.method).toBe('POST');
      request.flush({
        id: 'a-1',
        status: 'Confirmed',
        message: 'Done. Your mail was sent.',
        reply: message(3, 'Assistant', 'Done. Your mail was sent.', { provider: null })
      });
      fixture.detectChanges();

      expect(q('[data-action-card="a-1"]')!.getAttribute('data-status')).toBe('Confirmed');
      expect(q('[data-status-label]')!.textContent).toContain('Done');
      expect(q('[data-action="confirm-action"]')).toBeNull();
      expect(el().textContent).toContain('Done. Your mail was sent.');
    });

    it('saves edits to the server before confirming and shows a server error in the editor', async () => {
      await withCard();
      click('[data-action="edit-action"]');
      const to = q<HTMLInputElement>('[data-editor] [data-field="to"]')!;
      expect(to.value).toBe('ravi@taskflow.local');
      const subject = q<HTMLInputElement>('[data-editor] [data-field="subject"]')!;
      subject.value = 'Changed subject';
      subject.dispatchEvent(new Event('input'));
      to.value = 'nobody@elsewhere.example';
      to.dispatchEvent(new Event('input'));

      click('[data-action="save-edit"]');
      const bad = http.expectOne('/api/v1/assistant/actions/a-1');
      expect(bad.request.method).toBe('PUT');
      expect(bad.request.body).toEqual({ to: ['nobody@elsewhere.example'], subject: 'Changed subject', body: 'It is in review.' });
      bad.flush({ detail: 'These are not active TaskFlow users: nobody@elsewhere.example.' }, { status: 400, statusText: 'Bad Request' });
      fixture.detectChanges();
      expect(q('[data-error="edit"]')!.textContent).toContain('nobody@elsewhere.example');

      to.value = 'ravi@taskflow.local';
      to.dispatchEvent(new Event('input'));
      click('[data-action="save-edit"]');
      http.expectOne('/api/v1/assistant/actions/a-1').flush({ ...mailCard(), fields: [mailCard().fields[0], { label: 'Subject', value: 'Changed subject' }] });
      fixture.detectChanges();

      expect(q('[data-editor]')).toBeNull();
      expect(q('[data-action-card="a-1"]')!.textContent).toContain('Changed subject');
      http.expectNone('/api/v1/assistant/actions/a-1/confirm');
    });

    it('cancels through the cancel endpoint', async () => {
      await withCard();

      click('[data-action="cancel-action"]');
      http.expectOne('/api/v1/assistant/actions/a-1/cancel').flush(mailCard('Cancelled'));
      fixture.detectChanges();

      expect(q('[data-status-label]')!.textContent).toContain('Cancelled');
    });

    it('shows an expired action as expired with no buttons', async () => {
      await setup({ messages: [message(2, 'Assistant', 'Prepared.', { action: mailCard('Expired') })] });
      click('[data-action="open-tfa"]');

      expect(q('[data-status-label]')!.textContent).toContain('Ask TFA again');
      expect(q('[data-action="confirm-action"]')).toBeNull();
    });

    it('labels a role change and a profile change by what they do', async () => {
      const role: AssistantPendingAction = {
        id: 'r-1', kind: 'ChangeUserRole', status: 'Pending', expiresAt: '2026-10-19T09:10:00Z', title: 'Change role',
        fields: [{ label: 'User', value: 'Priya' }, { label: 'Role', value: 'Employee → Manager' }], body: null, canEdit: false
      };
      await setup({ messages: [message(2, 'Assistant', 'Prepared.', { action: role })] });
      click('[data-action="open-tfa"]');

      expect(q('[data-action="confirm-action"]')!.textContent).toContain('Change role');
      expect(q('[data-action="edit-action"]')).toBeNull();
    });
  });

  describe('provider failures', () => {
    it('shows the message in the chat and offers a retry and the other providers, never switching on its own', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      type('hello');
      click('[data-action="send"]');
      http.expectOne('/api/v1/assistant/conversation/messages').flush({
        messages: [],
        failure: {
          kind: 'RateLimited',
          provider: 'Ollama',
          message: 'Ollama is busy right now. Try again in about a minute, or choose another model.',
          retryAfterSeconds: null,
          alternatives: [gemini]
        }
      });
      fixture.detectChanges();

      expect(q('[data-failure-kind]')!.textContent).toContain('RateLimited');
      expect(q('[data-failure-message]')!.textContent).toContain('choose another model');
      expect(q('[data-action="retry"]')).not.toBeNull();
      expect(q('[data-action="use-Gemini"]')!.textContent).toContain('Cloud');
      expect(el().textContent).toContain('hello');
      http.expectNone('/api/v1/assistant/conversation/messages');

      click('[data-action="use-Gemini"]');
      // Gemini is a Cloud provider, so the user is asked first: the message is held, not sent.
      http.expectNone('/api/v1/assistant/conversation/messages');
      expect(q('[data-notice="cloud"]')).not.toBeNull();
    });

    it('retries with the same provider', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      type('hello');
      click('[data-action="send"]');
      http.expectOne('/api/v1/assistant/conversation/messages').flush({
        messages: [],
        failure: { kind: 'Timeout', provider: 'Ollama', message: 'Ollama took too long.', retryAfterSeconds: null, alternatives: [] }
      });
      fixture.detectChanges();

      click('[data-action="retry"]');

      const retry = http.expectOne('/api/v1/assistant/conversation/messages');
      expect(retry.request.body).toEqual({ text: 'hello', provider: 'Ollama' });
      retry.flush({ messages: [message(1, 'User', 'hello'), message(2, 'Assistant', 'hi')], failure: null });
    });
  });

  // ------------------------------------------------------------------ voice

  describe('voice', () => {
    it('does not use the microphone until the user turns voice on', async () => {
      await setup();
      click('[data-action="open-tfa"]');

      expect(recognition.starts).toBe(0);
      expect(q('[data-state="listening"]')).toBeNull();
      expect(q('[data-action="toggle-voice"]')!.getAttribute('aria-pressed')).toBe('false');
    });

    it('shows that it is listening once voice is on, and stops when it is turned off', async () => {
      await setup();
      click('[data-action="open-tfa"]');

      click('[data-action="toggle-voice"]');

      expect(recognition.starts).toBe(1);
      expect(q('[data-state="listening"]')!.textContent).toContain('Hey TFA');
      expect(q('[data-action="toggle-voice"]')!.getAttribute('aria-pressed')).toBe('true');

      click('[data-action="toggle-voice"]');
      expect(q('[data-state="listening"]')).toBeNull();
    });

    it('opens the panel on "Hey TFA" and puts the spoken question in the box without sending it', async () => {
      await setup();
      click('[data-action="toggle-voice"]');
      expect(q('[data-panel="tfa"]')!.classList).not.toContain('tfa--open');

      recognition.say('Hey TFA');
      fixture.detectChanges();
      expect(q('[data-panel="tfa"]')!.classList).toContain('tfa--open');
      expect(q('[data-state="listening"]')!.textContent).toContain('say your question');

      recognition.say('what should I prioritise today');
      fixture.detectChanges();

      expect(q<HTMLTextAreaElement>('[data-field="message"]')!.value).toBe('what should I prioritise today');
      http.expectNone('/api/v1/assistant/conversation/messages');
    });

    it('explains an unsupported browser and typing still works', async () => {
      await setup({ speech: false });
      click('[data-action="open-tfa"]');

      expect(q<HTMLButtonElement>('[data-action="toggle-voice"]')!.disabled).toBeTrue();
      expect(q('[data-voice-reason]')!.textContent).toContain('not supported');

      type('typed question');
      click('[data-action="send"]');
      expect(http.expectOne('/api/v1/assistant/conversation/messages').request.body.text).toBe('typed question');
    });

    it('explains a denied microphone and typing still works', async () => {
      await setup();
      click('[data-action="open-tfa"]');
      click('[data-action="toggle-voice"]');

      recognition.onerror?.({ error: 'not-allowed' });
      fixture.detectChanges();

      expect(q('[data-voice-reason]')!.textContent).toContain('Microphone access was denied');
      expect(q('[data-state="listening"]')).toBeNull();
      type('still typing');
      click('[data-action="send"]');
      expect(http.expectOne('/api/v1/assistant/conversation/messages').request.body.text).toBe('still typing');
    });

    it('starts listening again on a later visit only when voice had been turned on', async () => {
      localStorage.setItem(TFA_VOICE_KEY, 'on');
      await setup();

      expect(recognition.starts).toBe(1);
    });
  });

  it('starts a new chat from the header and clears the screen', async () => {
    await setup({ messages: [message(1, 'User', 'old'), message(2, 'Assistant', 'older')] });
    click('[data-action="open-tfa"]');

    click('[data-action="new-chat"]');
    http.expectOne('/api/v1/assistant/conversation/new').flush(null, { status: 204, statusText: 'No Content' });
    fixture.detectChanges();

    expect(el().textContent).not.toContain('older');
    expect(q('[data-welcome]')).not.toBeNull();
  });
});
