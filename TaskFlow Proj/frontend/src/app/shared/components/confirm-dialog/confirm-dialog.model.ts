export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel: string;
  variant?: 'primary' | 'danger';
  /** A notice to read, not a choice: only the confirm button is shown (for example "OK"). */
  notice?: boolean;
}
