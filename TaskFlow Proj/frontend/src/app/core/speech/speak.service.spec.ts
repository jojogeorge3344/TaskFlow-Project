import { fakeAsync, flush, TestBed, tick } from '@angular/core/testing';
import { MAX_SPOKEN_CHARS, speakingLimitMs, SPEECH_SYNTHESIS_FACTORY, SpeakService, UTTERANCE_FACTORY, toSpeech } from './speak.service';
import { SpeechSynthesisLike } from './speak.model';

type FakeUtterance = { text: string; lang: string; onend: (() => void) | null; onerror: (() => void) | null };

class FakeSynthesis implements SpeechSynthesisLike {
  utterances: FakeUtterance[] = [];
  cancels = 0;

  speak(utterance: SpeechSynthesisUtterance): void {
    this.utterances.push(utterance as unknown as FakeUtterance);
  }

  cancel(): void {
    this.cancels++;
  }
}

describe('SpeakService', () => {
  let synthesis: FakeSynthesis;

  function create(available = true): SpeakService {
    TestBed.configureTestingModule({
      providers: [
        { provide: SPEECH_SYNTHESIS_FACTORY, useValue: () => (available ? synthesis : null) },
        { provide: UTTERANCE_FACTORY, useValue: (text: string) => ({ text, lang: '', onend: null, onerror: null }) }
      ]
    });
    return TestBed.inject(SpeakService);
  }

  beforeEach(() => {
    synthesis = new FakeSynthesis();
  });

  it('reads the text aloud and calls done when it ends', () => {
    const service = create();
    let done = 0;

    service.speak('You have two tasks.', () => done++);

    expect(synthesis.utterances.map((utterance) => utterance.text)).toEqual(['You have two tasks.']);
    expect(done).toBe(0);
    synthesis.utterances[0].onend?.();
    expect(done).toBe(1);
  });

  it('carries on when the browser never reports that speaking ended, so the microphone is not left off', fakeAsync(() => {
    const service = create();
    let done = 0;

    service.speak('A short reply.', () => done++);
    tick(speakingLimitMs('A short reply.') - 1);
    expect(done).toBe(0);
    tick(2);

    expect(done).toBe(1);
    flush();
  }));

  it('does not wait for the safety limit when speaking ends normally', fakeAsync(() => {
    const service = create();
    let done = 0;
    service.speak('A short reply.', () => done++);

    synthesis.utterances[0].onend?.();
    tick(speakingLimitMs('A short reply.') + 1000);

    expect(done).toBe(1);
  }));

  it('calls done only once, even if the engine reports an error after the end', () => {
    const service = create();
    let done = 0;
    service.speak('Hello there', () => done++);

    synthesis.utterances[0].onend?.();
    synthesis.utterances[0].onerror?.();

    expect(done).toBe(1);
  });

  it('calls done at once and says nothing when there is no speech engine', () => {
    const service = create(false);
    let done = 0;

    service.speak('Hello', () => done++);

    expect(service.supported).toBeFalse();
    expect(done).toBe(1);
  });

  it('says nothing for text with nothing to read', () => {
    const service = create();
    let done = 0;

    service.speak('  ** ', () => done++);

    expect(synthesis.utterances.length).toBe(0);
    expect(done).toBe(1);
  });

  it('cancels what is being said', () => {
    const service = create();

    service.cancel();

    expect(synthesis.cancels).toBe(1);
  });

  describe('toSpeech', () => {
    it('drops markdown marks, reads links by their label and drops bare addresses', () => {
      expect(toSpeech('**Due today:** see [Task List](/tasks) or https://example.com/x now')).toBe('Due today: see Task List or now');
    });

    it('keeps a long reply to whole sentences within the limit', () => {
      const long = 'This is a sentence. '.repeat(60);

      const spoken = toSpeech(long);

      expect(spoken.length).toBeLessThanOrEqual(MAX_SPOKEN_CHARS);
      expect(spoken.endsWith('.')).toBeTrue();
    });
  });
});
