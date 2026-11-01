import { AiProviderConfig } from '../../../assistant/models/assistant.model';

/** One model in the TFA Settings list, with what its provider says about the key or address. */
export interface ModelRow {
  provider: AiProviderConfig;
  model: string;
  /** Added by an administrator, so it can be edited and deleted. Built-in models are read-only. */
  added: boolean;
  /** The model TFA answers with: the one saved on the enabled provider. */
  inUse: boolean;
  /** The provider has what a call needs: a readable key for a cloud provider; Ollama always has an address. */
  ready: boolean;
}

/** The rows of one provider, shown under its name. */
export interface ProviderGroup {
  provider: AiProviderConfig;
  rows: ModelRow[];
}
