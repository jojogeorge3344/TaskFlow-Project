import { FormControl, FormGroup } from '@angular/forms';

export type ProviderForm = FormGroup<{
  modelName: FormControl<string>;
  newModel: FormControl<string>;
  baseUrl: FormControl<string>;
  apiKey: FormControl<string>;
}>;
