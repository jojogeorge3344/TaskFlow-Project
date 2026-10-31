/** A recipient chip. `valid` is null while the address is being checked against TaskFlow users. */
export interface RecipientChip {
  email: string;
  name: string | null;
  valid: boolean | null;
}
