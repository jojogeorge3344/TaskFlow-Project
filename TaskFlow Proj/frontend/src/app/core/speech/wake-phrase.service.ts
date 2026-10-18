import { Injectable, InjectionToken, OnDestroy, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';

/** The few parts of the browser's SpeechRecognition that are used (the DOM typings do not include it). */
export interface SpeechRecognitionResultLike {
  readonly isFinal: boolean;
  readonly length: number;
  readonly [index: number]: { readonly transcript: string };
}

export interface SpeechRecognitionEventLike {
  readonly resultIndex: number;
  readonly results: { readonly length: number; readonly [index: number]: SpeechRecognitionResultLike };
}

export interface SpeechRecognitionLike {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  onresult: ((event: SpeechRecognitionEventLike) => void) | null;
  onerror: ((event: { readonly error: string }) => void) | null;
  onend: (() => void) | null;
  start(): void;
  stop(): void;
  abort(): void;
}

export type SpeechRecognitionFactory = () => SpeechRecognitionLike | null;

/** Creates the browser's recognizer, or null where there is none (Firefox and others). Replaced in tests. */
export const SPEECH_RECOGNITION_FACTORY = new InjectionToken<SpeechRecognitionFactory>('SPEECH_RECOGNITION_FACTORY', {
  providedIn: 'root',
  factory: () => () => {
    const host = window as unknown as Record<string, (new () => SpeechRecognitionLike) | undefined>;
    const Recognition = host['SpeechRecognition'] ?? host['webkitSpeechRecognition'];
    return Recognition ? new Recognition() : null;
  }
});

export const TFA_VOICE_KEY = 'tfa.voice';
export const WAKE_CAPTURE_TIMEOUT_MS = 8000;
const RESTART_DELAY_MS = 300;

/** "Hey TFA" as speech recognition tends to write it. */
const WAKE_PHRASE = /\bhey[\s,.!]*(?:tfa|t\.?\s*f\.?\s*a\.?|tea\s*eff\s*ay|tee\s*eff\s*ay)\b[\s,.!:;-]*/i;

/** Voice input for TFA. It is off until the user turns it on, and says clearly when it is listening. While on, it
 * waits for the phrase "Hey TFA", announces it, and then hands over the spoken question. Nothing is sent: the question
 * only fills the message box and the user submits it. Typing always works, whatever happens here. */
@Injectable({ providedIn: 'root' })
export class WakePhraseService implements OnDestroy {
  private readonly factory = inject(SPEECH_RECOGNITION_FACTORY);
  private recognition: SpeechRecognitionLike | null = null;
  private captureTimer: ReturnType<typeof setTimeout> | null = null;
  private restartTimer: ReturnType<typeof setTimeout> | null = null;

  /** True when this browser has speech recognition at all. */
  readonly supported = (() => {
    this.recognition = this.factory();
    return this.recognition !== null;
  })();

  /** The user's choice. Voice is off until they turn it on, and the choice is remembered. */
  readonly enabled = signal(this.supported && readStored(TFA_VOICE_KEY) === 'on');

  /** True while the microphone is actually listening. */
  readonly listening = signal(false);

  /** Why voice is unavailable or stopped, in words for the user. */
  readonly reason = signal<string | null>(this.supported ? null : 'Voice input is not supported in this browser. Try Chrome or Edge, or type your question.');

  /** After "Hey TFA" the next thing said is the question. */
  readonly capturing = signal(false);

  /** Emits when "Hey TFA" is heard, so the panel can open. */
  readonly wake = new Subject<void>();

  /** Emits the spoken question. It is never sent automatically. */
  readonly question = new Subject<string>();

  /** Starts listening again after a page load when the user had voice on. */
  resume(): void {
    if (this.enabled()) {
      this.begin();
    }
  }

  toggle(): void {
    if (this.enabled()) {
      this.disable();
    } else {
      this.enable();
    }
  }

  enable(): void {
    if (!this.supported) {
      this.reason.set('Voice input is not supported in this browser. Try Chrome or Edge, or type your question.');
      return;
    }

    this.reason.set(null);
    this.enabled.set(true);
    writeStored(TFA_VOICE_KEY, 'on');
    this.begin();
  }

  disable(): void {
    this.enabled.set(false);
    writeStored(TFA_VOICE_KEY, 'off');
    this.end();
  }

  ngOnDestroy(): void {
    this.end();
  }

  // ------------------------------------------------------------------ recognition

  private begin(): void {
    const recognition = this.recognition ?? this.factory();
    if (!recognition) {
      return;
    }

    this.recognition = recognition;
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = 'en-US';
    recognition.onresult = (event) => this.onResult(event);
    recognition.onerror = (event) => this.onError(event.error);
    recognition.onend = () => this.onEnd();

    try {
      recognition.start();
      this.listening.set(true);
    } catch {
      // Already started: nothing to do.
    }
  }

  private end(): void {
    this.clearTimers();
    this.capturing.set(false);
    this.listening.set(false);
    const recognition = this.recognition;
    if (recognition) {
      recognition.onresult = null;
      recognition.onerror = null;
      recognition.onend = null;
      try {
        recognition.abort();
      } catch {
        // Not running.
      }
    }
  }

  private onResult(event: SpeechRecognitionEventLike): void {
    for (let index = event.resultIndex; index < event.results.length; index++) {
      const result = event.results[index];
      const text = (result[0]?.transcript ?? '').trim();
      if (text) {
        this.handle(text, result.isFinal);
      }
    }
  }

  private handle(text: string, isFinal: boolean): void {
    if (!this.capturing()) {
      if (!WAKE_PHRASE.test(text)) {
        return;
      }

      // "Hey TFA" opens the panel straight away, even before the sentence is finished.
      this.capturing.set(true);
      this.wake.next();
      this.armCaptureTimeout();
    }

    if (!isFinal) {
      return;
    }

    const question = text.replace(WAKE_PHRASE, '').trim();
    if (question) {
      this.question.next(question);
      this.stopCapturing();
    } else {
      this.armCaptureTimeout();
    }
  }

  private onError(error: string): void {
    switch (error) {
      case 'not-allowed':
      case 'service-not-allowed':
        this.fail('Microphone access was denied. Allow it in your browser to use voice, or type your question.');
        break;
      case 'audio-capture':
        this.fail('No microphone was found. You can type your question instead.');
        break;
      case 'network':
        this.fail('Voice needs an internet connection in this browser. You can type your question instead.');
        break;
      default:
        // "no-speech" and "aborted" are normal while waiting.
        break;
    }
  }

  private onEnd(): void {
    this.listening.set(false);
    if (!this.enabled()) {
      return;
    }

    // Browsers stop listening after a pause; keep going while the user has voice on.
    this.restartTimer = setTimeout(() => this.begin(), RESTART_DELAY_MS);
  }

  private fail(reason: string): void {
    this.reason.set(reason);
    this.disable();
  }

  private armCaptureTimeout(): void {
    if (this.captureTimer) {
      clearTimeout(this.captureTimer);
    }

    this.captureTimer = setTimeout(() => this.stopCapturing(), WAKE_CAPTURE_TIMEOUT_MS);
  }

  private stopCapturing(): void {
    if (this.captureTimer) {
      clearTimeout(this.captureTimer);
      this.captureTimer = null;
    }

    this.capturing.set(false);
  }

  private clearTimers(): void {
    this.stopCapturing();
    if (this.restartTimer) {
      clearTimeout(this.restartTimer);
      this.restartTimer = null;
    }
  }
}

function readStored(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Storage can be blocked; the choice then lasts only for this visit.
  }
}
