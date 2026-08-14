import type { FeatureFlagsCacheStore } from '../cache.js';
import type { ResolvedOptions } from '../options.js';
import type { FlagSnapshot } from './evaluation.js';

/**
 * `${cacheKeyPrefix}${host}:${environment}:evaluation` — host and environment, not just the
 * prefix, so two environments (or two installations) sharing one store don't overwrite each
 * other's snapshot under the same key.
 *
 * The environment segment comes from the SDK key itself (`ffs_{env}_{selector}_{secret}`), known
 * synchronously at client construction — before the first fetch has told us the environment the
 * server actually reports, which is what a cache key derived any other way would have to wait for.
 * This is namespacing only, never trusted for anything the server itself decides: an unexpected
 * segment just means a cache miss, not a wrong answer.
 */
export function buildCacheKey(resolved: ResolvedOptions): string {
  const host = new URL(resolved.baseAddress).host;
  const environment = parseEnvironment(resolved.sdkKey);

  return `${resolved.cacheKeyPrefix}${host}:${environment}:evaluation`;
}

function parseEnvironment(sdkKey: string): string {
  const segments = sdkKey.split('_');

  return segments.length > 1 && segments[1] ? segments[1] : 'unknown';
}

/** The JSON shape written to a `FeatureFlagsCacheStore` — `FlagSnapshot` with its `Map` flattened
 * to a plain object, since `Map` does not survive `JSON.stringify` on its own. */
interface StoredSnapshot {
  environment: string;
  flags: Record<string, boolean>;
  etag: string | null;
  fetchedAt: number;
}

export function serializeSnapshot(snapshot: FlagSnapshot): string {
  const stored: StoredSnapshot = {
    environment: snapshot.environment,
    flags: Object.fromEntries(snapshot.flags),
    etag: snapshot.etag,
    fetchedAt: snapshot.fetchedAt,
  };

  return JSON.stringify(stored);
}

/** Parses what `serializeSnapshot` wrote. Never throws — a store is the consumer's own Redis (or
 * whatever else), not the FeatureFlags server, so a value it cannot make sense of is treated as a
 * miss rather than a client failure. */
export function deserializeSnapshot(value: string): FlagSnapshot | null {
  try {
    const parsed: unknown = JSON.parse(value);

    if (!isStoredSnapshot(parsed)) {
      return null;
    }

    const entries = Object.entries(parsed.flags).filter(
      (entry): entry is [string, boolean] => typeof entry[1] === 'boolean',
    );

    return {
      environment: parsed.environment,
      flags: new Map(entries),
      etag: parsed.etag,
      fetchedAt: parsed.fetchedAt,
    };
  } catch {
    return null;
  }
}

function isStoredSnapshot(value: unknown): value is StoredSnapshot {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<StoredSnapshot>;

  return (
    typeof candidate.environment === 'string' &&
    typeof candidate.fetchedAt === 'number' &&
    (candidate.etag === null || typeof candidate.etag === 'string') &&
    typeof candidate.flags === 'object' &&
    candidate.flags !== null
  );
}

/** Reads the last snapshot a store holds, swallowing every failure: a blip in the consumer's own
 * Redis is not the FeatureFlags origin being unreachable, and should not read as one. */
export async function readFromStore(
  store: FeatureFlagsCacheStore,
  key: string,
): Promise<FlagSnapshot | null> {
  try {
    const value = await store.get(key);

    return value === null ? null : deserializeSnapshot(value);
  } catch {
    return null;
  }
}

/** Writes a snapshot to a store, swallowing every failure for the same reason. */
export async function writeToStore(
  store: FeatureFlagsCacheStore,
  key: string,
  snapshot: FlagSnapshot,
  ttlSeconds: number,
): Promise<void> {
  try {
    await store.set(key, serializeSnapshot(snapshot), ttlSeconds);
  } catch {
    // Swallowed deliberately, same as the read above.
  }
}
