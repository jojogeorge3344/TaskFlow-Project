import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import {
  SPEECH_RECOGNITION_FACTORY,
  SpeechRecognitionEventLike,
  SpeechRecognitionLike,
  TFA_VOICE_KEY,
  WAKE_CAPTURE_TIMEOUT_MS,
  WakePhraseService
} from './wake-phrase.service';

class FakeRecognition implements SpeechRecognitionLike {
  continuous = false;
  interimResults = false;
  lang = '';
  onresult: ((event: SpeechRecognitionEventLike) => void) | null = null;
  onerror: ((event: { error: string }) => void) | null = null;
  onend: (() => void) | null = null;
  starts = 0;
  aborted = false;

  start(): void {
    this.starts++;
  }

  stop(): void {
    this.onend?.();
  }

  abort(): void {
    this.aborted = true;
  }

  say(transcript: string, isFinal = true): void {
    this.onresult?.({ resultIndex: 0, results: { length: 1, 0: { isFinal, length: 1, 0: { transcript } } } });
  }
}

describe('WakePhraseService', () => {
  let recognition: FakeRecognition;

  function create(factory: () => SpeechRecognitionLike | null = () => recognition): WakePhraseService {
    TestBed.configureTestingModule({ providers: [{ provide: SPEECH_RECOGNITION_FACTORY, useValue: factory }] });
    return TestBed.inject(WakePhraseService);
  }

  beforeEach(() => {
    localStorage.removeItem(TFA_VOICE_KEY);
    recognition = new FakeRecognition();
  });

  afterEach(() => localStorage.removeItem(TFA_VOICE_KEY));

  it('is off by default: the microphone is not used until the user turns voice on', () => {
    const service = create();

    expect(service.supported).toBeTrue();
    expect(service.enabled()).toBeFalse();
    expect(service.listening()).toBeFalse();
    service.resume();
    expect(recognition.starts).toBe(0);
  });

  it('listens once the user turns voice on, and remembers the choice', () => {
    const service = create();

    service.enable();

    expect(service.enabled()).toBeTrue();
    expect(service.listening()).toBeTrue();
    expect(recognition.starts).toBe(1);
    expect(recognition.continuous).toBeTrue();
    expect(localStorage.getItem(TFA_VOICE_KEY)).toBe('on');
  });

  it('resumes listening on a later visit only when the user had turned voice on', () => {
    localStorage.setItem(TFA_VOICE_KEY, 'on');
    const service = create();

    service.resume();

    expect(service.enabled()).toBeTrue();
    expect(recognition.starts).toBe(1);
  });

  it('opens on "Hey TFA" and hands over the question that follows, without sending it', () => {
    const service = create();
    const wakes: number[] = [];
    const questions: string[] = [];
    service.wake.subscribe(() => wakes.push(1));
    service.question.subscribe((question) => questions.push(question));
    service.enable();

    recognition.say('Hey TFA');
    expect(wakes.length).toBe(1);
    expect(service.capturing()).toBeTrue();
    expect(questions).toEqual([]);

    recognition.say('what should I prioritise today');

    expect(questions).toEqual(['what should I prioritise today']);
    expect(service.capturing()).toBeFalse();
    expect(wakes.length).toBe(1);
  });

  it('takes a question said in the same breath as the wake phrase', () => {
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));
    service.enable();

    recognition.say('Hey TFA, what are my upcoming tasks?');

    expect(questions).toEqual(['what are my upcoming tasks?']);
  });

  it('opens at once on an unfinished phrase and does not open twice for the finished one', () => {
    const service = create();
    let wakes = 0;
    service.wake.subscribe(() => wakes++);
    service.enable();

    recognition.say('hey tfa', false);
    recognition.say('hey tfa what is due', true);

    expect(wakes).toBe(1);
  });

  it('recognises common ways of writing the phrase', () => {
    const service = create();
    let wakes = 0;
    service.wake.subscribe(() => wakes++);
    service.enable();

    for (const phrase of ['hey tfa', 'Hey, TFA!', 'hey t f a', 'hey tea eff ay', 'HEY T.F.A.']) {
      recognition.say(phrase, false);
      recognition.say('ok', true);
    }

    expect(wakes).toBe(5);
  });

  it('ignores speech that does not contain the wake phrase', () => {
    const service = create();
    let wakes = 0;
    service.wake.subscribe(() => wakes++);
    service.enable();

    recognition.say('what is for lunch');
    recognition.say('they said hello to tfa yesterday');

    expect(wakes).toBe(0);
    expect(service.capturing()).toBeFalse();
  });

  it('stops waiting for a question after a while', fakeAsync(() => {
    const service = create();
    service.enable();
    recognition.say('hey tfa');
    expect(service.capturing()).toBeTrue();

    tick(WAKE_CAPTURE_TIMEOUT_MS + 1);

    expect(service.capturing()).toBeFalse();
  }));

  it('keeps listening after the browser stops, while voice is on', fakeAsync(() => {
    const service = create();
    service.enable();

    recognition.onend?.();
    expect(service.listening()).toBeFalse();
    tick(400);

    expect(recognition.starts).toBe(2);
    expect(service.listening()).toBeTrue();
  }));

  it('stops listening and does not restart once the user turns voice off', fakeAsync(() => {
    const service = create();
    service.enable();

    service.disable();
    tick(1000);

    expect(service.enabled()).toBeFalse();
    expect(service.listening()).toBeFalse();
    expect(recognition.aborted).toBeTrue();
    expect(recognition.starts).toBe(1);
    expect(localStorage.getItem(TFA_VOICE_KEY)).toBe('off');
  }));

  it('explains an unsupported browser and never starts anything', () => {
    const service = create(() => null);

    service.enable();

    expect(service.supported).toBeFalse();
    expect(service.enabled()).toBeFalse();
    expect(service.reason()).toContain('not supported');
    expect(service.reason()).toContain('type your question');
  });

  it('explains a denied microphone and turns voice off', () => {
    const service = create();
    service.enable();

    recognition.onerror?.({ error: 'not-allowed' });

    expect(service.enabled()).toBeFalse();
    expect(service.reason()).toContain('Microphone access was denied');
    expect(service.reason()).toContain('type your question');
  });

  it('explains a missing microphone and a network problem', () => {
    const service = create();
    service.enable();
    recognition.onerror?.({ error: 'audio-capture' });
    expect(service.reason()).toContain('No microphone');

    service.enable();
    recognition.onerror?.({ error: 'network' });
    expect(service.reason()).toContain('internet connection');
  });

  it('treats silence as normal', () => {
    const service = create();
    service.enable();

    recognition.onerror?.({ error: 'no-speech' });

    expect(service.enabled()).toBeTrue();
    expect(service.reason()).toBeNull();
  });
});
