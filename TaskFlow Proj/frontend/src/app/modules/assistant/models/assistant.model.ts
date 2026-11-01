import { TaskPriority, TaskStatus } from '../../monitoring/task-management/models/task.model';

/** How TaskFlow talks to a provider. Google Gemini, OpenAI GPT and Ollama are built in; a custom provider uses the
 * OpenAI-compatible ('OpenAI') or Ollama API at its own address. */
export type AiApiType = 'Ollama' | 'Gemini' | 'OpenAI';
export type AiLocality = 'Local' | 'Cloud';

/** A provider a user can choose in the model selector. Only configured providers are listed. */
export interface AiProviderOption {
  providerId: number;
  displayName: string;
  modelName: string;
  locality: AiLocality;
}

/** The administrator's view of one provider, built-in or custom. The API key is never included. */
export interface AiProviderConfig {
  providerId: number;
  displayName: string;
  apiType: AiApiType;
  isBuiltIn: boolean;
  locality: AiLocality;
  requiresKey: boolean;
  isEnabled: boolean;
  modelName: string;
  baseUrl: string | null;
  keySet: boolean;
  keyLast4: string | null;
  isConfigured: boolean;
  /** False when a key is stored but can no longer be decrypted, so it has to be entered again. */
  keyReadable: boolean;
  /** The models to choose from, kept in the database. The chosen model is always in the list. */
  models: string[];
  /** The models an administrator added, which can be removed (built-in ones cannot). */
  customModels: string[];
}

/** One provider's changes. An empty `apiKey` keeps the stored key; `clearKey` removes it. */
export interface UpdateAiProvider {
  providerId: number;
  isEnabled: boolean;
  modelName: string;
  baseUrl?: string | null;
  apiKey?: string | null;
  clearKey?: boolean;
}

export type ProviderFailureKind =
  | 'RateLimited'
  | 'InvalidKey'
  | 'Timeout'
  | 'Unreachable'
  | 'ModelUnavailable'
  | 'Unexpected'
  | 'InvalidModelName';

export interface AiProviderTestResult {
  ok: boolean;
  message: string;
  failureKind: ProviderFailureKind | null;
}

/** A provider typed on the Add or Edit AI model screen that does not exist yet; created only when the test passes. */
export interface NewAiProvider {
  name: string;
  /** 'OpenAI' (OpenAI-compatible) or 'Ollama'. */
  apiType: AiApiType;
  baseUrl: string;
  locality: AiLocality;
}

/** A model to add from the Add AI model page, or the body of an edit. The provider is `providerId` (an existing one) or
 * `newProvider`; on an edit, neither keeps the model's provider. An empty `apiKey` uses the provider's saved key;
 * `baseUrl` is an existing Ollama provider's address. */
export interface AddAiModelRequest {
  modelName: string;
  apiKey?: string | null;
  baseUrl?: string | null;
  /** Edit only: removes the provider's key without a test, and without other changes. */
  clearKey?: boolean;
  providerId?: number | null;
  newProvider?: NewAiProvider | null;
}

/** The outcome of adding a model: when `ok` is false nothing was saved and `providers` is null. */
export interface AddAiModelResult extends AiProviderTestResult {
  providers: AiProviderConfig[] | null;
}

export type AssistantMessageKind = 'Text' | 'Refusal' | 'Notice';
export type AssistantActionKind = 'SendMail' | 'UpdateOwnProfile' | 'ChangeUserRole';
export type AssistantActionStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Expired' | 'Refused' | 'Failed';

export interface AssistantLink {
  label: string;
  route: string;
}

/** A document section TFA was given for an answer: the document title, the section heading path and the file path. */
export interface AssistantSource {
  document: string;
  section: string;
  path: string;
}

export interface AssistantRankedTask {
  rank: number;
  taskId: number;
  taskCode: string;
  title: string;
  priority: TaskPriority;
  status: TaskStatus;
  deadline: string;
  dueDate: string;
  daysUntilDue: number;
  stage: 'Upcoming' | 'DueToday' | 'Overdue' | 'Completed';
  isBlocked: boolean;
}

export interface AssistantActionField {
  label: string;
  value: string;
}

/** A prepared action, drawn from the stored payload and never from model text. */
export interface AssistantPendingAction {
  id: string;
  kind: AssistantActionKind;
  status: AssistantActionStatus;
  expiresAt: string;
  title: string;
  fields: AssistantActionField[];
  body: string | null;
  canEdit: boolean;
}

export interface AssistantMessage {
  id: number;
  role: 'User' | 'Assistant';
  content: string;
  kind: AssistantMessageKind;
  createdAt: string;
  providerId: number | null;
  /** The name of the provider that answered; null for the user's own messages. */
  provider: string | null;
  links: AssistantLink[];
  rankedTasks: AssistantRankedTask[];
  action: AssistantPendingAction | null;
  /** The document sections given to TFA for this answer; empty when it did not use the documents. */
  sources: AssistantSource[];
  /** Client only: a message shown before the server has answered. */
  local?: boolean;
  /** Client only: a problem shown in the chat that did not come from the model. */
  problem?: boolean;
}

export interface AssistantConversation {
  conversationId: number | null;
  messages: AssistantMessage[];
}

/** A provider problem, already worded for the user. */
export interface AssistantFailure {
  kind: ProviderFailureKind;
  providerId: number;
  providerName: string;
  message: string;
  retryAfterSeconds: number | null;
  alternatives: AiProviderOption[];
}

export interface AssistantReply {
  messages: AssistantMessage[];
  failure: AssistantFailure | null;
}

export interface AssistantActionResult {
  id: string;
  status: AssistantActionStatus;
  message: string;
  reply: AssistantMessage | null;
}

export interface EditMailAction {
  to?: string[];
  subject?: string;
  body?: string;
}
