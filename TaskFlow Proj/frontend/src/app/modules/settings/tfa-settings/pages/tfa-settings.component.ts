import { Component } from '@angular/core';
import { AiProviderSettingsComponent } from '../components/ai-provider-settings.component';
import { DocumentIndexCardComponent } from '../components/document-index-card.component';

/** TFA Settings, under the Settings module: where the Administrator chooses the model and server TFA uses, adds models,
 * enters API keys and sees the document index TFA answers from. Opening it needs settings.ai.view; editing, testing,
 * removing models and re-indexing need their own keys. */
@Component({
  selector: 'app-tfa-settings',
  standalone: true,
  imports: [AiProviderSettingsComponent, DocumentIndexCardComponent],
  template: `
    <section class="tf-page" data-page="tfa-settings">
      <header class="tf-page-header">
        <div>
          <h1>TFA settings</h1>
          <p>Choose the AI model and server behind TFA, the TaskFlow assistant, add models, manage the API keys, and see the documents TFA answers from.</p>
        </div>
      </header>

      <app-ai-provider-settings />
      <app-document-index-card />
    </section>
  `
})
export class TfaSettingsComponent {}
