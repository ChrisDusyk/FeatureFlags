import { describe, expect, it } from 'vitest';

import { createFeatureFlagsClient } from '../src/index.js';
import { FakeCacheStore } from './fake-cache-store.js';
import { StubServer } from './stub-server.js';

const KEY = 'ffp_dev_b182276126b759aa_7f097037aa14d671f4317df877989f05f5309c1323ecb24dab4be559';
const BASE = 'https://flags.example.com';
const CACHE_KEY = 'featureflags:evaluation';

function serialized(flags: Record<string, boolean>, etag: string, fetchedAt: number): string {
  return JSON.stringify({ environment: 'dev', flags, etag, fetchedAt });
}

describe('the optional cache store', () => {
  it('answers from a still-fresh cached snapshot without calling the origin at all', async () => {
    const cache = new FakeCacheStore().seed(CACHE_KEY, serialized({ on: true }, '"v1"', Date.now()));
    const server = new StubServer().unreachable();

    const flags = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: KEY,
      fetch: server.fetch,
      cache,
      pollingInterval: 60_000,
    });

    // A cold process, pointed at a server that refuses every request — the whole point of the
    // store is that this still answers correctly.
    expect(await flags.isEnabled('on')).toBe(true);
    expect(server.callCount).toBe(0);

    flags.close();
  });

  it('uses a stale cached snapshot as the conditional-request baseline rather than discarding it', async () => {
    const cache = new FakeCacheStore().seed(
      CACHE_KEY,
      serialized({ on: true }, '"v1"', Date.now() - 120_000),
    );
    const server = new StubServer().notModified();

    const flags = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: KEY,
      fetch: server.fetch,
      cache,
      pollingInterval: 60_000,
    });

    expect(await flags.isEnabled('on')).toBe(true);
    expect(server.requests[0]?.headers.get('if-none-match')).toBe('"v1"');

    flags.close();
  });

  it('falls back to a normal fetch when the cached value cannot be parsed', async () => {
    const cache = new FakeCacheStore().seed(CACHE_KEY, 'not json');
    const server = new StubServer().withFlags({ on: true }, '"v1"');

    const flags = createFeatureFlagsClient({ baseAddress: BASE, sdkKey: KEY, fetch: server.fetch, cache });

    expect(await flags.isEnabled('on')).toBe(true);
    expect(server.callCount).toBe(1);

    flags.close();
  });

  it('writes a successful fetch through to the store, with the configured ttl', async () => {
    const cache = new FakeCacheStore();
    const server = new StubServer().withFlags({ on: true }, '"v1"');

    const flags = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: KEY,
      fetch: server.fetch,
      cache,
      cacheTtlSeconds: 3_600,
    });

    await flags.isEnabled('on');

    expect(cache.has(CACHE_KEY)).toBe(true);
    expect(cache.lastTtlSeconds).toBe(3_600);

    flags.close();
  });

  it('does not rewrite the store on a 304 — the value there is still correct', async () => {
    const cache = new FakeCacheStore().seed(
      CACHE_KEY,
      serialized({ on: true }, '"v1"', Date.now() - 120_000),
    );
    const server = new StubServer().notModified();

    const flags = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: KEY,
      fetch: server.fetch,
      cache,
      pollingInterval: 60_000,
    });

    await flags.isEnabled('on');

    expect(cache.setCalls).toBe(0);

    flags.close();
  });

  it('never lets a failing store surface through isEnabled', async () => {
    const cache = new FakeCacheStore();
    cache.failGet = true;
    cache.failSet = true;

    const server = new StubServer().withFlags({ on: true }, '"v1"');

    const flags = createFeatureFlagsClient({ baseAddress: BASE, sdkKey: KEY, fetch: server.fetch, cache });

    // isEnabled's contract is "never rejects" regardless of what's wrong; a store failure is not
    // the FeatureFlags origin being unreachable and must not read as one.
    expect(await flags.isEnabled('on')).toBe(true);

    flags.close();
  });

  it('behaves exactly as before this existed when no cache is configured', async () => {
    const server = new StubServer().withFlags({ on: true }, '"v1"');
    const flags = createFeatureFlagsClient({ baseAddress: BASE, sdkKey: KEY, fetch: server.fetch });

    expect(await flags.isEnabled('on')).toBe(true);

    flags.close();
  });
});
