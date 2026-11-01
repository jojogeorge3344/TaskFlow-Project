import { Component, inject, signal } from '@angular/core';
import { AiProviderOption } from '../models/assistant.model';
import { AssistantService } from '../services/assistant.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';

const BUILT_IN_STYLES: Record<string, string> = { Ollama: 'Ollama', 'Google Gemini': 'Gemini', 'OpenAI GPT': 'OpenAI' };

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
    this.assistant.selectProvider(option.providerId);
    this.menuOpen.set(false);
  }

  closeMenu(): void {
    this.menuOpen.set(false);
  }

  /** The badge colour: one for each built-in provider, and one shared by custom providers. */
  providerStyle(name: string): string {
    return BUILT_IN_STYLES[name] ?? 'Custom';
  }

  /** The badge letters for a provider: "AI" for OpenAI GPT, otherwise the first letter of its name (custom ones too). */
  providerInitial(name: string): string {
    return name.startsWith('OpenAI') ? 'AI' : name.charAt(0).toUpperCase();
  }

  newChat(): void {
    this.assistant.newChat();
  }
}
