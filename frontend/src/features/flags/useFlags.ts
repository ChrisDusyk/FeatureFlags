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
   *
   * Takes the environment the change belongs to, and ignores it if that is no longer the one on
   * screen — see the guard in the implementation for why that matters.
   */
  replace: (forEnvironmentKey: string, flag: Flag) => void;
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

  const replace = useCallback(
    (forEnvironmentKey: string, flag: Flag) => {
      // A toggle still in flight when the environment changes comes back answering for the
      // environment it started in. The flag has the same id in both, so applying it here would
      // write development's isEnabled and timestamp onto the production row now on screen.
      if (forEnvironmentKey !== environmentKey) {
        return;
      }

      setState((current) =>
        current.status === 'ready'
          ? {
              status: 'ready',
              flags: current.flags.map((candidate) => (candidate.id === flag.id ? flag : candidate)),
            }
          : current,
      );
    },
    [environmentKey],
  );

  return { state, reload, replace };
}
