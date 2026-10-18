import { Component, ElementRef, HostListener, OnInit, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AiProviderKind } from '../models';
import { AssistantService } from '../services/assistant.service';
import { WakePhraseService } from '../speech/wake-phrase.service';
import { IconComponent } from './icon.component';
import { TfaHeaderComponent } from './tfa-header.component';
import { TfaComposerComponent } from './tfa-composer.component';
import { TfaMessageComponent } from './tfa-message.component';

/** TFA, the assistant: a small panel docked to the right of every signed-in screen, opened by the floating button, by
 * typing, or by saying "Hey TFA". Everything it shows comes from the server; confirming or refusing an action is always
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
  private readonly voice = inject(WakePhraseService);

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

    // "Hey TFA" opens the panel; the spoken question lands in the message box and waits for the user to send it.
    this.voice.wake.pipe(takeUntilDestroyed()).subscribe(() => this.assistant.open());
    this.voice.question.pipe(takeUntilDestroyed()).subscribe((question) => {
      this.assistant.open();
      this.draft.set(question);
      setTimeout(() => this.composer()?.focus());
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
