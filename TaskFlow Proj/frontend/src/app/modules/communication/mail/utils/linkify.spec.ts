import { linkify, sameOriginPath } from './linkify';

describe('linkify', () => {
  it('leaves a body without addresses as one text part', () => {
    expect(linkify('Hello\nthere')).toEqual([{ text: 'Hello\nthere' }]);
  });

  it('turns an http(s) address into a link part and keeps the surrounding text', () => {
    expect(linkify('Open this task: http://localhost:4200/tasks/42\nThanks')).toEqual([
      { text: 'Open this task: ' },
      { url: 'http://localhost:4200/tasks/42' },
      { text: '\nThanks' }
    ]);
  });

  it('leaves a sentence\'s closing punctuation out of the link', () => {
    expect(linkify('See https://example.test/a/b.')).toEqual([{ text: 'See ' }, { url: 'https://example.test/a/b' }, { text: '.' }]);
  });

  it('handles several addresses', () => {
    const parts = linkify('a http://x.test/1 b https://y.test/2 c');
    expect(parts.filter((part) => part.url).map((part) => part.url)).toEqual(['http://x.test/1', 'https://y.test/2']);
  });

  it('does not link other schemes', () => {
    expect(linkify('javascript:alert(1) and ftp://x.test/f')).toEqual([{ text: 'javascript:alert(1) and ftp://x.test/f' }]);
  });
});

describe('sameOriginPath', () => {
  it('returns the in-app path for an address on this origin', () => {
    expect(sameOriginPath('http://localhost:4200/tasks/42#top', 'http://localhost:4200')).toBe('/tasks/42#top');
  });

  it('returns null for another origin or an invalid address', () => {
    expect(sameOriginPath('https://example.test/x', 'http://localhost:4200')).toBeNull();
    expect(sameOriginPath('not a url', 'http://localhost:4200')).toBeNull();
  });
});
