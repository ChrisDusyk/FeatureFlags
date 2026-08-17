import { SecretKeyInBrowserError } from './errors.js';
import { buildCacheKey, readFromStore, writeToStore } from './internal/cache-store.js';
import { fetchEvaluation, type FlagSnapshot } from './internal/evaluation.js';
import { deadline, isBrowser, unref } from './internal/runtime.js';
import { resolveOptions, SECRET_KEY_PREFIX, type FeatureFlagsOptions } from './options.js';

/**
 * Reads feature flags for the environment its SDK key is scoped to.
 *
 * Reads are served from an in-memory snapshot, so `isEnabled` on a hot path is a map lookup rather
 * than a request.
 */
export interface FeatureFlagClient {
  /**
   * Whether a flag is on. A key this installation has never heard of is `false` — a flag that does
   * not exist is not one that is on — unless a default is given.
   *
   * Never rejects. If the flags have never loaded, or the last refresh failed, the answer is the
   * last good one or the default: a flag service being unreachable should not take down everything
   * that reads it.
   */
  isEnabled(key: string, defaultValue?: boolean): Promise<boolean>;

  /**
   * Refetches now, rather than waiting for the polling interval. Unlike the background refresh,
   * this rejects when the fetch fails — an explicit request reports what happened.
   *
   * Every failure is a `FeatureFlagsError`: a refused key, an error status, an unreadable body, a
   * server that could not be reached, or one that did not answer in time. One type to catch,
   * whichever of those it was.
   */
  refresh(): Promise<void>;

  /**
   * Stops polling and abandons any request in flight. Idempotent. The client keeps answering from
   * its last snapshot afterwards; it simply stops asking for a new one.
   */
  close(): void;
}

export function createFeatureFlagsClient(options: FeatureFlagsOptions): FeatureFlagClient {
  const resolved = resolveOptions(options);

  // Before anything else, and before any request. By the time a 401 could tell us this, the key is
  // already in a bundle somebody downloaded — the useful moment to say so is now, in development,
  // at the line that configured it.
  if (isBrowser() && resolved.sdkKey.startsWith(SECRET_KEY_PREFIX)) {
    throw new SecretKeyInBrowserError();
  }

  let snapshot: FlagSnapshot | null = null;
  let inFlight: Promise<void> | null = null;
  let closed = false;

  // Aborts whatever is in flight when close() is called, so a pending fetch cannot keep a process
  // alive or land after the caller has finished with the client.
  const lifetime = new AbortController();

  const cacheKey = buildCacheKey(resolved);

  // When the store was last written to. A 304 doesn't change what's there, so it doesn't need a
  // fresh write every poll — but it does need one occasionally, or a store whose ttlSeconds is
  // shorter than "how long this process goes between real changes" would expire a perfectly
  // current entry, and a restart during that gap would find nothing. Rewriting at half the TTL
  // keeps it always at most half-expired without writing on every single poll.
  let cacheWrittenAt = 0;

  async function load(): Promise<void> {
    const attempt = deadline(lifetime.signal, resolved.timeout);

    try {
      // A cold process has nothing yet — this is the one case a store actually changes behavior
      // for. If what it holds is still within the polling interval, it is trusted outright and the
      // origin is not asked at all; a process that starts already knowing its flags is the entire
      // point of adding a store. If it is older than that, it is still handed to fetchEvaluation
      // below as the conditional-request baseline, so an unchanged answer still costs only a 304.
      if (snapshot === null && resolved.cache) {
        const cached = await readFromStore(resolved.cache, cacheKey);

        if (cached && Date.now() - cached.fetchedAt < resolved.pollingInterval) {
          snapshot = cached;

          return;
        }

        snapshot = cached;
      }

      const fetched = await fetchEvaluation(resolved, snapshot, attempt);

      // Null is a 304: the answer is unchanged, so only its age moves. Without re-stamping, an
      // unchanged snapshot would look stale forever and be refetched on every read.
      snapshot = fetched ?? (snapshot ? { ...snapshot, fetchedAt: Date.now() } : null);

      if (snapshot && resolved.cache) {
        const dueForRewrite = Date.now() - cacheWrittenAt >= (resolved.cacheTtlSeconds * 1000) / 2;

        if (fetched || dueForRewrite) {
          await writeToStore(resolved.cache, cacheKey, snapshot, resolved.cacheTtlSeconds);
          cacheWrittenAt = Date.now();
        }
      }
    } finally {
      attempt.settle();
    }
  }

  /**
   * One refresh at a time. Twenty callers finding the snapshot stale at once should produce one
   * request, and the nineteen that lost should use what the winner fetched.
   */
  function refresh(): Promise<void> {
    if (closed) {
      return Promise.resolve();
    }

    inFlight ??= load().finally(() => {
      inFlight = null;
    });

    return inFlight;
  }

  async function isEnabled(key: string, defaultValue = false): Promise<boolean> {
    if (typeof key !== 'string') {
      throw new TypeError('FeatureFlags: isEnabled needs a flag key.');
    }

    const stale = snapshot === null || Date.now() - snapshot.fetchedAt >= resolved.pollingInterval;

    if (stale && !closed) {
      // Swallowed deliberately: the caller wants an answer, and the last good one — or their
      // default — is a better answer than a rejected promise.
      await refresh().catch(() => {});
    }

    return snapshot?.flags.get(key.toLowerCase()) ?? defaultValue;
  }

  const timer = setInterval(() => {
    void refresh().catch(() => {
      // A polling loop that stopped on one bad response would leave the snapshot frozen at whatever
      // it last held, silently, for the life of the process.
    });
  }, resolved.pollingInterval);

  unref(timer);

  // Kick off the first load rather than waiting for the first read, so an application that starts
  // and then serves a request immediately does not pay for the fetch on that request.
  //
  // The rejection is swallowed because nothing is waiting on it: an unhandled one would take a Node
  // process down over exactly the outage this client exists to survive. A caller who would rather
  // fail fast awaits `refresh()` themselves, which does report — that is the fail-fast path here,
  // and it is a plain await rather than an option that has to be discovered.
  void refresh().catch(() => {});

  return {
    isEnabled,
    refresh,
    close(): void {
      if (closed) {
        return;
      }

      closed = true;
      clearInterval(timer);
      lifetime.abort();
    },
  };
}
