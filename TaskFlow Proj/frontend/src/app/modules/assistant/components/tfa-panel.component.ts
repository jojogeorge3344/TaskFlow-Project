import { Component, ElementRef, HostListener, OnInit, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AiProviderKind, AssistantMessage } from '../models/assistant.model';
import { AuthService } from '../../../shared/auth/auth.service';
import { AssistantService } from '../services/assistant.service';
import { SpeakService } from '../services/speak.service';
import { WakePhraseService } from '../services/wake-phrase.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { TfaHeaderComponent } from './tfa-header.component';
import { TfaComposerComponent } from './tfa-composer.component';
import { TfaMessageComponent } from './tfa-message.component';

/** TFA, the assistant: a small panel docked to the right of every signed-in screen, opened by the floating button, by
 * typing, or by saying "Hey TF assistant". Everything it shows comes from the server; confirming or refusing an action is always
 * the server's decision. */
@Component({
  selector: 'app-tfa-panel',
  standalone: true,
  imports: [IconComponent, TfaHeaderComponent, TfaMessageComponent, TfaComposerComponent],
  templateUrl: './tfa-panel.component.html',
  styleUrl: './tfa-panel.component.css'
})
export class TfaPanelComponent implements OnInit {
  readonly assistant = inject(AssistantService);
  readonly voice = inject(WakePhraseService);
  private readonly auth = inject(AuthService);
  private readonly speaker = inject(SpeakService);

  /** True from a spoken question until its reply has been read aloud: typed questions get text only. */
  private voiceAsked = false;

  private readonly composer = viewChild(TfaComposerComponent);
  private readonly header = viewChild(TfaHeaderComponent);
  private readonly feed = viewChild<ElementRef<HTMLElement>>('feed');

  readonly draft = signal('');

  constructor() {
    // Opening the panel puts the cursor in the message box.
    effect(() => {
      if (this.assistant.panelOpen()) {
        setTimeout(() => this.composer()?.focus());
      }
    });

    // The newest message stays in view.
    effect(() => {
      this.assistant.messages();
      this.assistant.sending();
      this.assistant.failed();
      setTimeout(() => {
        const element = this.feed()?.nativeElement;
        if (element) {
          element.scrollTop = element.scrollHeight;
        }
      });
    });

    // A spoken question is answered in text and aloud.
    effect(() => {
      const messages = this.assistant.messages();
      const failed = this.assistant.failed();
      const sending = this.assistant.sending();
      untracked(() => this.answerAloud(messages, failed?.failure.message ?? null, sending));
    });

    // Turning voice off also stops anything being read out.
    effect(() => {
      if (!this.voice.enabled()) {
        this.voiceAsked = false;
        this.speaker.cancel();
      }
    });

    // Speaking opens the panel and fills the box live; the finished question is sent and answered in text and aloud.
    this.voice.wake.pipe(takeUntilDestroyed()).subscribe(() => {
      this.speaker.cancel();
      this.assistant.open();
    });
    // "Hey TF assistant" on its own: greet the user by name, then listen for the command.
    this.voice.greet.pipe(takeUntilDestroyed()).subscribe(() => {
      this.assistant.open();
      this.voiceAsked = false;
      this.voice.pause();
      this.speaker.speak(greeting(this.auth.currentUser()?.fullName), () => this.voice.unpause(true));
    });
    this.voice.interim.pipe(takeUntilDestroyed()).subscribe((heard) => this.draft.set(heard));
    this.voice.question.pipe(takeUntilDestroyed()).subscribe((question) => {
      this.assistant.open();
      if (this.assistant.available() && !this.assistant.sending()) {
        this.draft.set('');
        this.voiceAsked = true;
        this.assistant.send(question);
      } else {
        this.draft.set(question);
        setTimeout(() => this.composer()?.focus());
      }
    });
  }

  ngOnInit(): void {
    this.voice.resume();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    const header = this.header();
    if (header?.menuOpen()) {
      header.closeMenu();
    } else if (this.assistant.panelOpen()) {
      this.assistant.close();
    }
  }

  /** Reads the newest reply aloud when it answers a spoken question. The microphone rests while TFA talks, so it does
   * not hear its own voice. */
  private answerAloud(messages: AssistantMessage[], failure: string | null, sending: boolean): void {
    if (!this.voiceAsked || sending) {
      return;
    }

    const last = messages.at(-1);
    const text = failure ?? (last && last.role === 'Assistant' ? last.content : null);
    if (!text) {
      return;
    }

    this.voiceAsked = false;
    this.voice.pause();
    this.speaker.speak(text, () => this.voice.unpause());
  }

  /** Retries the failed message with a model the user picks on the failure card. */
  useProvider(kind: AiProviderKind): void {
    this.assistant.selectProvider(kind);
    this.assistant.retry();
  }

  acknowledge(): void {
    this.assistant.acknowledgeCloud();
  }

  /** Not sending to a Cloud provider after all: the text goes back into the message box. */
  keepLocal(): void {
    const held = this.assistant.discardHeld();
    if (held) {
      this.draft.set(held);
    }
  }
}

/** "Hi Asha, how can I help you?" with the signed-in user's first name. */
export function greeting(fullName: string | null | undefined): string {
  const first = (fullName ?? '').trim().split(/\s+/)[0];
  return first ? `Hi ${first}, how can I help you?` : 'Hi, how can I help you?';
}
