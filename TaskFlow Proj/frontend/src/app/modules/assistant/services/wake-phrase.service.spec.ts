import { fakeAsync, flush, TestBed, tick } from '@angular/core/testing';
import { SPEECH_RECOGNITION_FACTORY, COMMAND_SILENCE_MS, DICTATION_TIMEOUT_MS, TFA_VOICE_KEY, WAKE_CAPTURE_TIMEOUT_MS, WakePhraseService } from './wake-phrase.service';
import { SpeechRecognitionEventLike, SpeechRecognitionLike } from './wake-phrase.model';

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

  it('is on by default: it listens as soon as the app starts it', () => {
    const service = create();

    expect(service.supported).toBeTrue();
    expect(service.enabled()).toBeTrue();
    service.resume();
    expect(recognition.starts).toBe(1);
    expect(service.listening()).toBeTrue();
  });

  it('stays off after the user turned it off: the microphone is not used', () => {
    localStorage.setItem(TFA_VOICE_KEY, 'off');
    const service = create();

    expect(service.enabled()).toBeFalse();
    service.resume();
    expect(recognition.starts).toBe(0);
    expect(service.listening()).toBeFalse();
  });

  it('rests the microphone on pause and listens again on unpause, without turning voice off', () => {
    const service = create();
    service.resume();

    service.pause();
    expect(service.listening()).toBeFalse();
    expect(service.enabled()).toBeTrue();
    expect(recognition.aborted).toBeTrue();

    service.unpause();
    expect(service.listening()).toBeTrue();
    expect(recognition.starts).toBe(2);
  });

  it('does not listen again on unpause when voice was turned off meanwhile', () => {
    const service = create();
    service.resume();
    service.pause();
    service.disable();

    service.unpause();

    expect(recognition.starts).toBe(1);
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

  it('resumes listening on a later visit unless the user had turned voice off', () => {
    localStorage.setItem(TFA_VOICE_KEY, 'on');
    const service = create();

    service.resume();

    expect(service.enabled()).toBeTrue();
    expect(recognition.starts).toBe(1);
  });

  it('opens on "Hey TF assistant" and hands over the question that follows, without sending it', fakeAsync(() => {
    const service = create();
    const wakes: number[] = [];
    const questions: string[] = [];
    service.wake.subscribe(() => wakes.push(1));
    service.question.subscribe((question) => questions.push(question));
    service.enable();

    recognition.say('Hey TF assistant');
    expect(wakes.length).toBe(1);
    expect(service.capturing()).toBeTrue();
    expect(questions).toEqual([]);

    recognition.say('what should I prioritise today');
    expect(questions).toEqual([]);
    tick(COMMAND_SILENCE_MS);

    expect(questions).toEqual(['what should I prioritise today']);
    expect(service.capturing()).toBeFalse();
    expect(wakes.length).toBe(1);
  }));

  it('takes a question said in the same breath as the wake phrase', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));
    service.enable();

    recognition.say('Hey TF assistant, what are my upcoming tasks?');

    tick(COMMAND_SILENCE_MS);

    expect(questions).toEqual(['what are my upcoming tasks?']);
  }));

  it('opens at once on an unfinished phrase and does not open twice for the finished one', () => {
    const service = create();
    let wakes = 0;
    service.wake.subscribe(() => wakes++);
    service.enable();

    recognition.say('hey tf assistant', false);
    recognition.say('hey tf assistant what is due', true);

    expect(wakes).toBe(1);
  });

  it('recognises common ways of writing the phrase', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));

    for (const phrase of ['hey tf assistant', 'Hey, TF assistant!', 'hey t f assistant', 'hey tea eff assistant', 'HEY T.F. ASSISTANT']) {
      service.enable();
      recognition.say(phrase + ' ok', true);
      tick(COMMAND_SILENCE_MS);
    }

    expect(questions).toEqual(['ok', 'ok', 'ok', 'ok', 'ok']);
  }));

  it('recognises usual mishearings of the phrase', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));

    for (const phrase of ['Hey Tifa assistant', 'hay TFA assistant', 'hey TF Assistant', 'Hey Teefa assistant']) {
      service.enable();
      recognition.say(phrase + ' ok', true);
      tick(COMMAND_SILENCE_MS);
    }

    expect(questions).toEqual(['ok', 'ok', 'ok', 'ok']);
  }));

  it('keeps voice on after a reload when the browser needs a click, and starts at the next click', fakeAsync(() => {
    localStorage.setItem(TFA_VOICE_KEY, 'on');
    const service = create();
    service.resume();

    recognition.onerror?.({ error: 'not-allowed' });
    recognition.onend?.();
    tick(1000);

    expect(service.enabled()).toBeTrue();
    expect(service.reason()).toContain('Click anywhere');
    expect(recognition.starts).toBe(1);

    document.dispatchEvent(new Event('pointerdown'));

    expect(recognition.starts).toBe(2);
    expect(service.listening()).toBeTrue();
    expect(service.reason()).toBeNull();
  }));

  it('closes the microphone by itself after a mic-button command when hands-free is off', fakeAsync(() => {
    localStorage.setItem(TFA_VOICE_KEY, 'off');
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));

    service.dictate();
    expect(service.listening()).toBeTrue();
    expect(service.dictating()).toBeTrue();

    recognition.say('what is due today', true);
    tick(COMMAND_SILENCE_MS);

    expect(questions).toEqual(['what is due today']);
    expect(service.dictating()).toBeFalse();
    expect(service.listening()).toBeFalse();
    expect(recognition.aborted).toBeTrue();
  }));

  it('closes the microphone when nothing is said after the mic button', fakeAsync(() => {
    localStorage.setItem(TFA_VOICE_KEY, 'off');
    const service = create();
    service.dictate();

    tick(DICTATION_TIMEOUT_MS + 1);

    expect(service.dictating()).toBeFalse();
    expect(service.listening()).toBeFalse();
  }));

  it('keeps listening for the wake phrase after a mic-button command when hands-free is on', fakeAsync(() => {
    const service = create();
    service.resume();
    service.dictate();

    recognition.say('what is due today', true);
    tick(COMMAND_SILENCE_MS);

    expect(service.dictating()).toBeFalse();
    expect(service.listening()).toBeTrue();
  }));

  it('asks for a greeting when "Hey TF assistant" is said on its own, but not when a question follows', fakeAsync(() => {
    const service = create();
    const greets: number[] = [];
    service.greet.subscribe(() => greets.push(1));
    service.resume();

    recognition.say('hey tf assistant what is due', true);
    tick(COMMAND_SILENCE_MS);
    expect(greets).toEqual([]);

    recognition.say('hey tf assistant', true);
    expect(greets).toEqual([1]);
  }));

  it('keeps what it heard, so it is clear what was understood', () => {
    const service = create();
    service.resume();

    recognition.say('hello there', false);

    expect(service.heard()).toBe('hello there');
  });

  it('does not remember a failure as the user turning hands-free off', () => {
    const service = create();
    service.resume();

    recognition.onerror?.({ error: 'network' });

    expect(service.enabled()).toBeFalse();
    expect(service.reason()).toContain('internet connection');
    expect(localStorage.getItem(TFA_VOICE_KEY)).toBeNull();
  });

  it('remembers when the user turns hands-free off', () => {
    const service = create();

    service.disable();

    expect(localStorage.getItem(TFA_VOICE_KEY)).toBe('off');
  });

  it('goes back to standby after a question, and then only reacts to "Hey TF assistant"', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));
    service.enable();
    service.dictate();

    recognition.say('what is due today', true);
    tick(COMMAND_SILENCE_MS);
    recognition.say('what is for lunch', true);
    tick(COMMAND_SILENCE_MS);
    expect(questions).toEqual(['what is due today']);
    expect(service.enabled()).toBeTrue();
    expect(service.capturing()).toBeFalse();

    recognition.say('hey tf assistant what should I do first', true);
    tick(COMMAND_SILENCE_MS);
    expect(questions).toEqual(['what is due today', 'what should I do first']);
  }));

  it('takes a question without the wake phrase right after the mic is clicked', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    const heard: string[] = [];
    service.question.subscribe((question) => questions.push(question));
    service.interim.subscribe((text) => heard.push(text));
    service.dictate();

    recognition.say('what is due', false);
    recognition.say('what is due today', true);

    expect(heard).toEqual(['what is due', 'what is due today']);
    expect(questions).toEqual([]);
    tick(COMMAND_SILENCE_MS);
    expect(questions).toEqual(['what is due today']);
  }));

  it('joins a long command that arrives in several pieces and sends it once, after the user stops', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    const heard: string[] = [];
    service.question.subscribe((question) => questions.push(question));
    service.interim.subscribe((text) => heard.push(text));
    service.resume();

    recognition.say('hey tf assistant I need to know', true);
    tick(COMMAND_SILENCE_MS - 500);
    recognition.say('which tasks are overdue', false);
    recognition.say('which tasks are overdue', true);
    tick(COMMAND_SILENCE_MS - 500);
    recognition.say('and who they belong to', true);

    expect(questions).toEqual([]);
    expect(heard.at(-1)).toBe('I need to know which tasks are overdue and who they belong to');
    expect(service.capturing()).toBeTrue();

    tick(COMMAND_SILENCE_MS);

    expect(questions).toEqual(['I need to know which tasks are overdue and who they belong to']);
    expect(service.capturing()).toBeFalse();
  }));

  it('does not mistake a later piece of the command for something to ignore, or a new command', fakeAsync(() => {
    const service = create();
    const questions: string[] = [];
    service.question.subscribe((question) => questions.push(question));
    service.resume();

    recognition.say('hey tf assistant first part', true);
    recognition.say('second part', true);
    tick(COMMAND_SILENCE_MS);
    recognition.say('chatter after the command', true);
    tick(COMMAND_SILENCE_MS);

    expect(questions).toEqual(['first part second part']);
  }));

  it('stops waiting for a question after a while', fakeAsync(() => {
    const service = create();
    service.enable();
    recognition.say('hey tf assistant');
    expect(service.capturing()).toBeTrue();

    tick(WAKE_CAPTURE_TIMEOUT_MS + 1);

    expect(service.capturing()).toBeFalse();
  }));

  it('keeps listening after the browser stops, without showing a gap', fakeAsync(() => {
    const service = create();
    service.enable();
    tick(5000);

    recognition.onend?.();
    expect(service.listening()).toBeTrue();
    tick(100);

    expect(recognition.starts).toBe(2);
    expect(service.listening()).toBeTrue();
  }));

  it('never opens a second session while one is running', () => {
    const service = create();
    service.resume();

    service.dictate();
    service.unpause();
    service.resume();

    expect(recognition.starts).toBe(1);
  });

  it('uses a fresh recognizer for each session, so a late event from an old one cannot confuse the new one', () => {
    const made: FakeRecognition[] = [];
    const service = create(() => {
      const next = new FakeRecognition();
      made.push(next);
      return next;
    });
    made.length = 0;
    service.resume();
    const first = made[0];

    service.pause();
    service.unpause();

    expect(made.length).toBe(2);
    expect(first.aborted).toBeTrue();
    expect(first.onend).toBeNull();
    expect(made[1].starts).toBe(1);
  });

  it('waits longer each time a session ends at once, then stops with a reason instead of flickering for ever', fakeAsync(() => {
    const service = create();
    service.enable();

    for (let attempt = 0; attempt < 20 && service.enabled(); attempt++) {
      recognition.onend?.();
      flush();
    }

    expect(service.enabled()).toBeFalse();
    expect(service.listening()).toBeFalse();
    expect(service.reason()).toContain('Voice keeps stopping');
    expect(recognition.starts).toBeLessThan(10);
    // Giving up is for this visit only: it is never saved as the user turning hands-free off.
    expect(localStorage.getItem(TFA_VOICE_KEY)).not.toBe('off');
  }));

  it('names the browser error when voice keeps stopping', fakeAsync(() => {
    const service = create();
    service.enable();

    for (let attempt = 0; attempt < 20 && service.enabled(); attempt++) {
      recognition.onerror?.({ error: 'language-not-supported' });
      recognition.onend?.();
      flush();
    }

    expect(service.reason()).toContain('language-not-supported');
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
