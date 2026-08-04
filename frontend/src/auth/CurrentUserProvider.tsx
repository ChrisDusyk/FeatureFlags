import { useEffect, useState, type ReactNode } from 'react';

import { CurrentUserContext, type CurrentUserState } from './currentUser';
import { apiFetch } from './token';

/**
 * Fetches the signed-in user from the API once, so every screen reads the same answer rather
 * than each decoding the token for itself.
 */
export function CurrentUserProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<CurrentUserState>({ status: 'loading' });

  useEffect(() => {
    const controller = new AbortController();

    apiFetch('/api/users/me', { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) {
          return { status: 'unavailable' } as const;
        }

        return { status: 'ready', user: await response.json() } as const;
      })
      .then(setState)
      .catch(() => {
        if (!controller.signal.aborted) {
          setState({ status: 'unavailable' });
        }
      });

    return () => controller.abort();
  }, []);

  return <CurrentUserContext.Provider value={state}>{children}</CurrentUserContext.Provider>;
}
