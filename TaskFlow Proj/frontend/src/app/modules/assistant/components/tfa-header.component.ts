import { Component, inject, signal } from '@angular/core';
import { AiProviderKind, AiProviderOption } from '../models/assistant.model';
import { AssistantService } from '../services/assistant.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';

/** The panel header: the TFA title, New chat, Close, and the model selector. The selector lists only the providers an
 * administrator has configured, each marked Local or Cloud. Switching model never closes the chat. */
@Component({
  selector: 'app-tfa-header',
  standalone: true,
  imports: [IconComponent],
  templateUrl: './tfa-header.component.html',
  styleUrl: './tfa-header.component.css'
})
export class TfaHeaderComponent {
  readonly assistant = inject(AssistantService);

  readonly menuOpen = signal(false);

  chooseProvider(option: AiProviderOption): void {
    this.assistant.selectProvider(option.kind);
    this.menuOpen.set(false);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  providerInitial(kind: AiProviderKind): string {
    return kind === 'OpenAI' ? 'AI' : kind.charAt(0);
  }

  newChat(): void {
    this.assistant.newChat();
  }
}
