/** A piece of a mail body: plain text, or a web address that should be shown as a link. */
export type BodyPart = { text: string; url?: undefined } | { url: string; text?: undefined };

const URL_PATTERN = /https?:\/\/[^\s<>"']+/g;
/** Punctuation that ends a sentence rather than the address ("see http://x/y."). */
const TRAILING_PUNCTUATION = /[.,;:!?)\]}]+$/;

/** Splits a plain-text mail body into text and http(s) links. Only http and https addresses become
 * links, and the result is rendered through Angular bindings (never as HTML), so a body cannot inject markup. */
export function linkify(body: string): BodyPart[] {
  const parts: BodyPart[] = [];
  let cursor = 0;

  for (const match of body.matchAll(URL_PATTERN)) {
    const start = match.index ?? 0;
    const raw = match[0];
    const url = raw.replace(TRAILING_PUNCTUATION, '');

    if (start > cursor) {
      parts.push({ text: body.slice(cursor, start) });
    }
    parts.push({ url });
    cursor = start + url.length;
  }

  if (cursor < body.length) {
    parts.push({ text: body.slice(cursor) });
  }

  return parts;
}

/** The in-app path (path, query and fragment) of an address on this application's own origin, or null for any
 * other address, so same-app links can navigate without reloading the page. */
export function sameOriginPath(url: string, origin: string): string | null {
  try {
    const parsed = new URL(url);
    return parsed.origin === origin ? `${parsed.pathname}${parsed.search}${parsed.hash}` : null;
  } catch {
    return null;
  }
}
