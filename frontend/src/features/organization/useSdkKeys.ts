import { useCallback, useEffect, useState } from 'react';

import { ApiError, listSdkKeys, type SdkKey } from './api';

export type SdkKeysState =
  | { status: 'loading' }
  | { status: 'ready'; keys: SdkKey[] }
  /** Signed in, but not an admin. The screen still has plenty to say without the list. */
  | { status: 'forbidden' }
  | { status: 'failed'; message: string };

export interface SdkKeysResult {
  state: SdkKeysState;
  reload: () => void;
}

/**
 * Every SDK key, in one list, grouped by environment on the screen rather than fetched per
 * environment. Unlike flags, keys are not something you look at one environment at a time — the
 * question is usually "what can reach this installation at all".
 */
export function useSdkKeys(): SdkKeysResult {
  const [state, setState] = useState<SdkKeysState>({ status: 'loading' });
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    setState({ status: 'loading' });

    listSdkKeys(controller.signal)
      .then((keys) => {
        if (!controller.signal.aborted) {
          setState({ status: 'ready', keys });
        }
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        // A 403 is not a failure to report — it is the answer. The console hides the controls for
        // a non-admin, but the server is what decides, and this is it deciding.
        if (cause instanceof ApiError && cause.status === 403) {
          setState({ status: 'forbidden' });
          return;
        }

        setState({
          status: 'failed',
          message: cause instanceof ApiError ? cause.message : 'The console could not reach the API.',
        });
      });

    return () => controller.abort();
  }, [reloadCount]);

  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  return { state, reload };
}
