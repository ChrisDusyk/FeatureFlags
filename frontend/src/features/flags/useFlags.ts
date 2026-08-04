import { useCallback, useEffect, useState } from 'react';

import { ApiError, listFlags, type Flag } from './api';

export type FlagsState =
  | { status: 'loading' }
  | { status: 'ready'; flags: Flag[] }
  | { status: 'failed'; message: string };

export interface FlagsResult {
  state: FlagsState;
  /** Re-reads the list. Used by the retry link and after a flag is created. */
  reload: () => void;
  /**
   * Replaces one flag in place, so a toggle can show its result immediately and put the old
   * value back if the server refuses.
   */
  replace: (flag: Flag) => void;
}

/**
 * The flags for one environment. Switching environments re-asks the API rather than filtering
 * what is already loaded — the server owns the answer, and a stale one about production is
 * exactly the kind of wrong worth a round trip to avoid.
 */
export function useFlags(environmentKey: string): FlagsResult {
  const [state, setState] = useState<FlagsState>({ status: 'loading' });
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    setState({ status: 'loading' });

    listFlags(environmentKey, controller.signal)
      .then((flags) => {
        if (!controller.signal.aborted) {
          setState({ status: 'ready', flags });
        }
      })
      .catch((cause: unknown) => {
        // Aborted means a newer request is already on its way, or the screen is gone. Either way
        // this answer is no longer wanted, and painting an error over it would be wrong.
        if (controller.signal.aborted) {
          return;
        }

        setState({
          status: 'failed',
          message: cause instanceof ApiError ? cause.message : 'The console could not reach the API.',
        });
      });

    return () => controller.abort();
  }, [environmentKey, reloadCount]);

  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  const replace = useCallback((flag: Flag) => {
    setState((current) =>
      current.status === 'ready'
        ? {
            status: 'ready',
            flags: current.flags.map((candidate) => (candidate.id === flag.id ? flag : candidate)),
          }
        : current,
    );
  }, []);

  return { state, reload, replace };
}
