import { TaskPriority, TaskStatus } from './task.model';

/** The AI providers TFA can use. Ollama runs locally; Gemini and OpenAI are cloud services. */
export type AiProviderKind = 'Ollama' | 'Gemini' | 'OpenAI';
export type AiLocality = 'Local' | 'Cloud';

export const AI_PROVIDER_KINDS: AiProviderKind[] = ['Ollama', 'Gemini', 'OpenAI'];

/** A provider a user can choose in the model selector. Only configured providers are listed. */
export interface AiProviderOption {
  kind: AiProviderKind;
  displayName: string;
  modelName: string;
  locality: AiLocality;
}

/** The administrator's view of one provider. The API key is never included. */
export interface AiProviderConfig {
  kind: AiProviderKind;
  displayName: string;
  locality: AiLocality;
  requiresKey: boolean;
  isEnabled: boolean;
  modelName: string;
  baseUrl: string | null;
  keySet: boolean;
  keyLast4: string | null;
  isConfigured: boolean;
}

/** One provider's changes. An empty `apiKey` keeps the stored key; `clearKey` removes it. */
export interface UpdateAiProvider {
  kind: AiProviderKind;
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
  | 'Unexpected';

export interface AiProviderTestResult {
  ok: boolean;
  message: string;
  failureKind: ProviderFailureKind | null;
}

export type AssistantMessageKind = 'Text' | 'Refusal' | 'Notice';
export type AssistantActionKind = 'SendMail' | 'UpdateOwnProfile' | 'ChangeUserRole';
export type AssistantActionStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Expired' | 'Refused' | 'Failed';

export interface AssistantLink {
  label: string;
  route: string;
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
  provider: AiProviderKind | null;
  links: AssistantLink[];
  rankedTasks: AssistantRankedTask[];
  action: AssistantPendingAction | null;
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
  provider: AiProviderKind;
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
