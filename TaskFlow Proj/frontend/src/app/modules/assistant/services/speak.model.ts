/** The few parts of the browser's speech synthesis that are used. */
export interface SpeechSynthesisLike {
  speak(utterance: SpeechSynthesisUtterance): void;
  cancel(): void;
}

export type SpeechSynthesisFactory = () => SpeechSynthesisLike | null;

export type UtteranceFactory = (text: string) => SpeechSynthesisUtterance;
