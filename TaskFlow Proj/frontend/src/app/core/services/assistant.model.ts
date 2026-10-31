import { AssistantFailure } from '../models';

/** A provider problem together with the text that was being sent, so it can be retried. */
export interface FailedSend {
  failure: AssistantFailure;
  text: string;
}
