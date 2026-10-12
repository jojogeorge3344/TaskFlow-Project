/** Values of the `reason` query parameter that tell the login screen why the user was signed out. */
export const LOGIN_REASON_SESSION_ENDED = 'session-ended';
export const LOGIN_REASON_CREDENTIALS_CHANGED = 'credentials-changed';

export const LOGIN_REASON_MESSAGES: Record<string, string> = {
  [LOGIN_REASON_SESSION_ENDED]:
    'Your session has ended, for example because your username or password was changed. Please sign in again.',
  [LOGIN_REASON_CREDENTIALS_CHANGED]: 'Your username or password was changed. Sign in again with your new details.'
};
