import { Injectable, InjectionToken, inject } from '@angular/core';
import { SpeechSynthesisFactory, UtteranceFactory } from './speak.model';

/** The browser's speech synthesis, or null where there is none. Replaced in tests. */
export const SPEECH_SYNTHESIS_FACTORY = new InjectionToken<SpeechSynthesisFactory>('SPEECH_SYNTHESIS_FACTORY', {
  providedIn: 'root',
  factory: () => () => ('speechSynthesis' in window ? window.speechSynthesis : null)
});

/** Builds the utterance. Replaced in tests, where there is no real speech engine. */
export const UTTERANCE_FACTORY = new InjectionToken<UtteranceFactory>('UTTERANCE_FACTORY', {
  providedIn: 'root',
  factory: () => (text) => new SpeechSynthesisUtterance(text)
});

/** Longest reply read aloud. A long answer is read in part: the rest is on screen. */
export const MAX_SPOKEN_CHARS = 500;

/** Reads TFA's replies aloud. Used only when the question was spoken, and always safe to call: with no speech engine it
 * does nothing and calls `done` at once. */
@Injectable({ providedIn: 'root' })
export class SpeakService {
  private readonly synthesis = inject(SPEECH_SYNTHESIS_FACTORY)();
  private readonly makeUtterance = inject(UTTERANCE_FACTORY);

  readonly supported = this.synthesis !== null;

  /** The utterance being said. Chrome can drop one it no longer sees referenced and then never report that it ended. */
  private current: SpeechSynthesisUtterance | null = null;
  private safetyTimer: ReturnType<typeof setTimeout> | null = null;

  /** Reads the text aloud, then calls `done` (also when it fails or is cancelled). */
  speak(text: string, done: () => void): void {
    const spoken = toSpeech(text);
    if (!this.synthesis || !spoken) {
      done();
      return;
    }

    try {
      this.synthesis.cancel();
      const utterance = this.makeUtterance(spoken);
      utterance.lang = 'en-US';
      let finished = false;
      const finish = () => {
        if (finished) {
          return;
        }

        finished = true;
        this.clearSafety();
        this.current = null;
        done();
      };
      utterance.onend = finish;
      utterance.onerror = finish;
      this.clearSafety();
      this.current = utterance;
      // If the browser never reports the end, carry on anyway so the microphone is not left off.
      this.safetyTimer = setTimeout(() => {
        this.cancel();
        finish();
      }, speakingLimitMs(spoken));
      this.synthesis.speak(utterance);
    } catch {
      done();
    }
  }

  private clearSafety(): void {
    if (this.safetyTimer) {
      clearTimeout(this.safetyTimer);
      this.safetyTimer = null;
    }
  }

  cancel(): void {
    try {
      this.synthesis?.cancel();
    } catch {
      // Nothing was being spoken.
    }
  }
}

/** The longest a reply can take to say, with room to spare, before speaking is treated as finished. */
export function speakingLimitMs(text: string): number {
  return Math.min(60_000, 5_000 + text.length * 120);
}

/** Turns a reply into something worth saying: no markdown marks, links read by their label, and a sensible length. */
export function toSpeech(text: string): string {
  const plain = text
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/https?:\/\/\S+/g, '')
    .replace(/[*_`#>|]/g, '')
    .replace(/\s+/g, ' ')
    .trim();

  if (plain.length <= MAX_SPOKEN_CHARS) {
    return plain;
  }

  const cut = plain.slice(0, MAX_SPOKEN_CHARS);
  const sentenceEnd = Math.max(cut.lastIndexOf('. '), cut.lastIndexOf('? '), cut.lastIndexOf('! '));
  return sentenceEnd > MAX_SPOKEN_CHARS / 2 ? cut.slice(0, sentenceEnd + 1) : cut;
}
