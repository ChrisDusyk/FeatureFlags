import { createContext, useContext } from 'react';

/**
 * The signed-in user as the *API* sees them, which is not quite the same thing as the session.
 * The session says who you are; this says what the server will let you do, because the role
 * here is the one it reads out of its own mirror of the identity. The console renders against
 * this so what it offers and what the API allows cannot disagree.
 */
export interface CurrentUser {
  id: string;
  email: string;
  name: string;
  role: string;
  isAdmin: boolean;
}

export type CurrentUserState =
  | { status: 'loading' }
  | { status: 'ready'; user: CurrentUser }
  /** Signed in, but the server would not tell us who — the console stays usable and unprivileged. */
  | { status: 'unavailable' };

export const CurrentUserContext = createContext<CurrentUserState | null>(null);

export function useCurrentUser(): CurrentUserState {
  const state = useContext(CurrentUserContext);

  if (!state) {
    throw new Error('useCurrentUser must be called inside a CurrentUserProvider.');
  }

  return state;
}
