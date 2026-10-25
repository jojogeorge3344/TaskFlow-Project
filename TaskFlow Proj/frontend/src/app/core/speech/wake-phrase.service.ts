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

/** Remembers only a deliberate choice to turn hands-free off. An error never writes it. */
export const TFA_VOICE_KEY = 'tfa.handsfree';
/** How long the mic waits for a command after the mic button is pressed, before it closes by itself. */
export const DICTATION_TIMEOUT_MS = 10000;
/** How long the user must be quiet before the command is considered finished and is sent. A long command arrives as
 * several pieces, one per pause for breath, so a short pause does not end it. */
export const COMMAND_SILENCE_MS = 2000;
export const WAKE_CAPTURE_TIMEOUT_MS = 8000;
/** The browser ends a listening session by itself after a quiet spell; the next one starts almost at once. */
const RESTART_NOW_MS = 50;
/** Backoff base for a session that ends straight away, which means something is wrong, not that it was quiet. */
const RESTART_DELAY_MS = 300;
const QUICK_END_MS = 1500;
const MAX_QUICK_ENDS = 6;

/** "Hey TF assistant" as speech recognition tends to write it ("Hey T F assistant", "Hay TFA assistant", "Hey Tifa assistant"). */
const WAKE_PHRASE = /\b(?:hey|hay|hei)[\s,.!]*(?:t[\s.]*f[\s.]*a?|t[ie]e?fa|t(?:ea|ee)?\s*eff?\s*a?)[\s,.!]*assistant\b[\s,.!:;-]*/i;

/** Voice input for TFA. Two ways in: hands-free, which waits for "Hey TF assistant" while the app is open, and the mic
 * button, which listens once for a command and then closes by itself. Either way the words fill the message box as they
 * are spoken and the finished command is handed over. Typing always works, whatever happens here. */
@Injectable({ providedIn: 'root' })
export class WakePhraseService implements OnDestroy {
  private readonly factory = inject(SPEECH_RECOGNITION_FACTORY);
  private recognition: SpeechRecognitionLike | null = null;
  private captureTimer: ReturnType<typeof setTimeout> | null = null;
  private restartTimer: ReturnType<typeof setTimeout> | null = null;
  private dictationTimer: ReturnType<typeof setTimeout> | null = null;
  private silenceTimer: ReturnType<typeof setTimeout> | null = null;
  /** The finished pieces of the command so far. The browser delivers a long command in several pieces. */
  private segments: string[] = [];
  /** True from the mic button until its command is finished, when "Hey TF assistant" is not needed. */
  private direct = false;
  /** True while listening was started by the page itself (no click), which browsers may refuse until the user acts. */
  private autostart = false;
  /** Set when the browser refused an automatic start; the next click or key press starts listening. */
  private gestureHandler: (() => void) | null = null;
  /** True while a browser listening session is open. Never start a second one on top of it. */
  private running = false;
  private startedAt = 0;
  /** Sessions in a row that ended straight away. */
  private quickEnds = 0;
  private lastError = '';

  /** True when this browser has speech recognition at all. */
  readonly supported = this.factory() !== null;

  /** Hands-free: listening for "Hey TF assistant". On unless the user has turned it off, and the choice is remembered. */
  readonly enabled = signal(this.supported && readStored(TFA_VOICE_KEY) !== 'off');

  /** True while the microphone is actually listening. */
  readonly listening = signal(false);

  /** True from a press of the mic button until its command is finished or given up. */
  readonly dictating = signal(false);

  /** The last words the microphone heard, so it is clear what was understood (and what was not). */
  readonly heard = signal('');

  /** Why voice is unavailable or stopped, in words for the user. */
  readonly reason = signal<string | null>(this.supported ? null : 'Voice input is not supported in this browser. Try Chrome or Edge, or type your question.');

  /** After "Hey TF assistant" the next thing said is the question. */
  readonly capturing = signal(false);

  /** Emits when "Hey TF assistant" is heard, so the panel can open. */
  readonly wake = new Subject<void>();

  /** Emits the words heard so far while the user is still speaking, so the message box fills live. */
  readonly interim = new Subject<string>();

  /** Emits when "Hey TF assistant" was said on its own, so TFA can greet the user and wait for the command. */
  readonly greet = new Subject<void>();

  /** Emits the finished spoken command. */
  readonly question = new Subject<string>();

  /** Starts listening again after a page load when the user had voice on. */
  resume(): void {
    if (this.enabled()) {
      this.autostart = true;
      this.quickEnds = 0;
      this.begin();
    }
  }

  /** Stops the microphone for a moment (while TFA speaks, so it does not hear itself) without turning voice off. */
  pause(): void {
    this.end();
  }

  /** Listens again after `pause`: for the command itself when `asCommand`, otherwise for the wake phrase if hands-free is on. */
  unpause(asCommand = false): void {
    if (asCommand) {
      this.dictate();
    } else if (this.enabled()) {
      // Not from a click: if the browser refuses, wait for the next click instead of turning voice off.
      this.autostart = true;
      this.quickEnds = 0;
      this.begin();
    }
  }

  /** The mic button: listens once for a command, with no wake phrase, then closes by itself. */
  dictate(): void {
    if (!this.supported) {
      this.reason.set('Voice input is not supported in this browser. Try Chrome or Edge, or type your question.');
      return;
    }

    this.reason.set(null);
    this.autostart = false;
    this.clearGestureWait();
    this.direct = true;
    this.dictating.set(true);
    this.quickEnds = 0;
    this.armDictationTimeout();
    this.begin();
  }

  /** Hands-free on or off. */
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
    this.autostart = false;
    this.enabled.set(true);
    writeStored(TFA_VOICE_KEY, 'on');
    this.quickEnds = 0;
    this.begin();
  }

  /** The user turns hands-free off: remembered. */
  disable(): void {
    writeStored(TFA_VOICE_KEY, 'off');
    this.stop();
  }

  private stop(): void {
    this.enabled.set(false);
    this.direct = false;
    this.dictating.set(false);
    this.end();
  }

  ngOnDestroy(): void {
    this.end();
  }

  // ------------------------------------------------------------------ recognition

  /** Opens a listening session, on a fresh recognizer each time. Reusing one lets the late "ended" event of an aborted
   * session land on the new one, which then looks stopped and is restarted over and over. */
  private begin(): void {
    if (this.running) {
      return;
    }

    this.clearRestart();
    const recognition = this.factory();
    if (!recognition) {
      return;
    }

    this.recognition = recognition;
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = 'en-US';
    recognition.onresult = (event) => {
      this.quickEnds = 0;
      this.onResult(event);
    };
    recognition.onerror = (event) => this.onError(event.error);
    recognition.onend = () => this.onEnd();

    try {
      recognition.start();
      this.running = true;
      this.startedAt = Date.now();
      this.listening.set(true);
    } catch {
      // The browser is not ready yet: try again while voice is wanted, but not for ever.
      this.running = false;
      if (this.wantsMicrophone()) {
        this.retryLater();
      }
    }
  }

  private wantsMicrophone(): boolean {
    return this.enabled() || this.dictating();
  }

  /** Tries again after a pause that grows, and gives up with a reason when it keeps failing. */
  private retryLater(): void {
    this.quickEnds++;
    if (this.quickEnds >= MAX_QUICK_ENDS) {
      const detail = this.lastError ? ` (${this.lastError})` : '';
      this.fail(`Voice keeps stopping${detail}. Close other apps or tabs that use the microphone, then press the mic button.`);
      return;
    }

    const delay = this.quickEnds <= 1 ? RESTART_NOW_MS : Math.min(RESTART_DELAY_MS * 2 ** (this.quickEnds - 2), 4000);
    this.clearRestart();
    this.restartTimer = setTimeout(() => this.begin(), delay);
  }

  private clearRestart(): void {
    if (this.restartTimer) {
      clearTimeout(this.restartTimer);
      this.restartTimer = null;
    }
  }

  private end(): void {
    this.clearTimers();
    this.clearGestureWait();
    this.capturing.set(false);
    this.listening.set(false);
    this.running = false;
    const recognition = this.recognition;
    this.recognition = null;
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
    this.heard.set(text);

    // After the mic button, any speech is the command. In standby only "Hey TF assistant" starts one.
    if (!this.capturing()) {
      if (!this.direct && !WAKE_PHRASE.test(text)) {
        return;
      }

      this.segments = [];
      this.capturing.set(true);
      this.wake.next();
    }

    // Any speech keeps the command alive; a pause long enough ends it.
    this.armCaptureTimeout();
    this.clearSilence();

    const piece = text.replace(WAKE_PHRASE, '').trim();
    if (!isFinal) {
      const said = [...this.segments, piece].filter(Boolean).join(' ');
      if (said) {
        this.interim.next(said);
      }
      return;
    }

    if (piece) {
      this.segments.push(piece);
      this.interim.next(this.segments.join(' '));
      this.armSilence();
    } else if (this.segments.length > 0) {
      this.armSilence();
    } else if (!this.direct) {
      // The wake phrase on its own: greet, then wait for the command.
      this.greet.next();
    }
  }

  /** Sends what was said, as one command, once the user has been quiet for a moment. */
  private armSilence(): void {
    this.clearSilence();
    this.silenceTimer = setTimeout(() => this.flush(), COMMAND_SILENCE_MS);
  }

  private clearSilence(): void {
    if (this.silenceTimer) {
      clearTimeout(this.silenceTimer);
      this.silenceTimer = null;
    }
  }

  private flush(): void {
    const command = this.segments.join(' ').trim();
    this.segments = [];
    if (command) {
      this.question.next(command);
    }

    this.finishCommand();
  }

  /** The command is done: back to waiting for the wake phrase, or closed if hands-free is off. */
  private finishCommand(): void {
    this.clearSilence();
    this.segments = [];
    this.direct = false;
    this.dictating.set(false);
    this.stopCapturing();
    if (this.dictationTimer) {
      clearTimeout(this.dictationTimer);
      this.dictationTimer = null;
    }

    if (!this.enabled()) {
      this.end();
    }
  }

  private onError(error: string): void {
    this.lastError = error;
    switch (error) {
      case 'not-allowed':
      case 'service-not-allowed':
        if (this.autostart) {
          // Voice was on from an earlier visit and the browser will not open the microphone without a click.
          this.waitForGesture();
          break;
        }

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
    this.running = false;
    if (!this.wantsMicrophone() || this.gestureHandler) {
      this.listening.set(false);
      return;
    }

    // The browser stops listening after a quiet spell. That is normal: listening carries on without showing a gap.
    // A session that ends at once is not: wait longer each time, and give up with a reason.
    if (Date.now() - this.startedAt >= QUICK_END_MS) {
      this.quickEnds = 0;
    }

    this.retryLater();
  }

  /** Keeps voice on and starts listening at the user's next click or key press, which the browser accepts. */
  private waitForGesture(): void {
    this.reason.set('Click anywhere on the page to turn "Hey TF assistant" back on.');
    if (this.gestureHandler) {
      return;
    }

    const handler = () => {
      this.clearGestureWait();
      this.autostart = false;
      this.reason.set(null);
      this.begin();
    };
    this.gestureHandler = handler;
    document.addEventListener('pointerdown', handler);
    document.addEventListener('keydown', handler);
  }

  private clearGestureWait(): void {
    const handler = this.gestureHandler;
    if (handler) {
      document.removeEventListener('pointerdown', handler);
      document.removeEventListener('keydown', handler);
      this.gestureHandler = null;
    }
  }

  /** Voice stops for this visit and says why. It is not remembered as the user's choice, so the next visit tries again. */
  private fail(reason: string): void {
    this.reason.set(reason);
    this.stop();
  }

  private armCaptureTimeout(): void {
    if (this.captureTimer) {
      clearTimeout(this.captureTimer);
    }

    this.captureTimer = setTimeout(() => this.giveUpCommand(), WAKE_CAPTURE_TIMEOUT_MS);
  }

  private armDictationTimeout(): void {
    if (this.dictationTimer) {
      clearTimeout(this.dictationTimer);
    }

    this.dictationTimer = setTimeout(() => {
      this.dictationTimer = null;
      if (!this.capturing()) {
        this.giveUpCommand();
      }
    }, DICTATION_TIMEOUT_MS);
  }

  /** Nothing was said in time: stop waiting for a command, and close the microphone if hands-free is off. */
  private giveUpCommand(): void {
    if (this.segments.length > 0) {
      this.flush();
    } else {
      this.finishCommand();
    }
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
    this.clearSilence();
    if (this.dictationTimer) {
      clearTimeout(this.dictationTimer);
      this.dictationTimer = null;
    }

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
