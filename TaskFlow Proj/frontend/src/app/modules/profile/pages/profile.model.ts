export type ProfileField = 'fullName' | 'email' | 'mobileNumber';

/** A read-only account fact. More than one value renders as a bulleted list; none renders as a dash. */
export interface ReadonlyField {
  label: string;
  values: string[];
  /** Shown beneath the values, e.g. the total allocation. */
  note?: string | null;
}
