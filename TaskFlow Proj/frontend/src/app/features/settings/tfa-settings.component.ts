import { Component } from '@angular/core';
import { AiProviderSettingsComponent } from './ai-provider-settings.component';

/** TFA Settings, under the Settings module: where the Administrator chooses the model and server TFA uses, adds models
 * and enters API keys. Only the Administrator (settings.ai.manage) can open it. */
@Component({
  selector: 'app-tfa-settings',
  standalone: true,
  imports: [AiProviderSettingsComponent],
  template: `
    <section class="tf-page" data-page="tfa-settings">
      <header class="tf-page-header">
        <div>
          <h1>TFA settings</h1>
          <p>Choose the AI model and server behind TFA, the TaskFlow assistant, add models, and manage the API keys.</p>
        </div>
      </header>

      <app-ai-provider-settings />
    </section>
  `
})
export class TfaSettingsComponent {}
