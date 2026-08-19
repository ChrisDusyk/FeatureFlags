import { useCallback, useEffect, useState } from 'react';

import { ApiError, getFlag, getFlagHistory, type FlagDetail, type FlagHistoryEntry } from './api';

export type FlagDetailState =
  | { status: 'loading' }
  | { status: 'ready'; flag: FlagDetail }
  | { status: 'failed'; message: string };

export type FlagHistoryState =
  | { status: 'loading' }
  | { status: 'ready'; entries: FlagHistoryEntry[] }
  | { status: 'failed'; message: string };

/**
 * A flag's details and its activity history — two independent fetches, so a slow or failed
 * history does not hold the edit form hostage, and vice versa.
 */
export function useFlagDetail(key: string) {
  const [detail, setDetail] = useState<FlagDetailState>({ status: 'loading' });
  const [history, setHistory] = useState<FlagHistoryState>({ status: 'loading' });
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setDetail({ status: 'loading' });

    getFlag(key, controller.signal)
      .then((flag) => {
        if (!controller.signal.aborted) {
          setDetail({ status: 'ready', flag });
        }
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        setDetail({
          status: 'failed',
          message: cause instanceof ApiError ? cause.message : 'The console could not reach the API.',
        });
      });

    return () => controller.abort();
  }, [key, reloadCount]);

  useEffect(() => {
    const controller = new AbortController();
    setHistory({ status: 'loading' });

    getFlagHistory(key, controller.signal)
      .then((entries) => {
        if (!controller.signal.aborted) {
          setHistory({ status: 'ready', entries });
        }
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted) {
          return;
        }

        setHistory({
          status: 'failed',
          message: cause instanceof ApiError ? cause.message : 'The console could not reach the API.',
        });
      });

    return () => controller.abort();
  }, [key, reloadCount]);

  // Re-runs both fetches. Used after a successful edit, so the newly-saved details and the
  // FlagDetailsChanged entry it produced both show up together.
  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  return { detail, history, reload };
}
