import { Component, ElementRef, computed, inject, model, viewChild } from '@angular/core';
import { AssistantService } from '../services/assistant.service';
import { WakePhraseService } from '../speech/wake-phrase.service';
import { IconComponent } from './icon.component';

export const TFA_SUGGESTIONS = [
  'What are my upcoming tasks?',
  'What should I prioritise?',
  'How do I change a task status?',
  'Send a mail to my manager'
];

/** The message box with the voice button and the starter suggestions. Voice only fills the box; the user sends. */
@Component({
  selector: 'app-tfa-composer',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './tfa-composer.component.html',
  styleUrl: './tfa-composer.component.css'
})
export class TfaComposerComponent {
  readonly assistant = inject(AssistantService);
  readonly voice = inject(WakePhraseService);

  private readonly box = viewChild<ElementRef<HTMLTextAreaElement>>('messageBox');

  /** The text in the message box, shared with the panel so a held or spoken message can be put back into it. */
  readonly draft = model('');

  readonly suggestions = TFA_SUGGESTIONS;
  readonly canSend = computed(() => this.draft().trim().length > 0 && !this.assistant.sending() && this.assistant.available());

  focus(): void {
    this.box()?.nativeElement.focus();
  }

  onInput(event: Event): void {
    const box = event.target as HTMLTextAreaElement;
    this.draft.set(box.value);
    this.grow(box);
  }

  onKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  send(): void {
    if (!this.canSend()) {
      return;
    }

    const text = this.draft();
    this.draft.set('');
    const box = this.box()?.nativeElement;
    if (box) {
      box.value = '';
      this.grow(box);
    }

    this.assistant.send(text);
  }

  useSuggestion(text: string): void {
    this.draft.set(text);
    this.focus();
  }

  private grow(box: HTMLTextAreaElement): void {
    box.style.height = 'auto';
    box.style.height = `${Math.min(box.scrollHeight, 96)}px`;
  }
}
