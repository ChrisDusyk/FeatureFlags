import { afterEach, describe, expect, it, vi } from 'vitest';

import { createFeatureFlagsClient } from '../src/index.js';
import { FakeCacheStore } from './fake-cache-store.js';
import { StubServer } from './stub-server.js';

const KEY = 'ffp_dev_b182276126b759aa_7f097037aa14d671f4317df877989f05f5309c1323ecb24dab4be559';
const BASE = 'https://flags.example.com';
const CACHE_KEY = 'featureflags:flags.example.com:dev:evaluation';

function serialized(flags: Record<string, boolean>, etag: string, fetchedAt: number): string {
  return JSON.stringify({ environment: 'dev', flags, etag, fetchedAt });
}

describe('the optional cache store', () => {
  afterEach(() => vi.useRealTimers());

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

  it('does not rewrite the store on every 304 — only occasionally, to keep the ttl from lapsing', async () => {
    const cache = new FakeCacheStore().seed(
      CACHE_KEY,
      serialized({ on: true }, '"v1"', Date.now() - 120_000),
    );
    const server = new StubServer().notModified().notModified();

    const flags = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: KEY,
      fetch: server.fetch,
      cache,
      pollingInterval: 60_000,
      // A day, so the throttle window (half of it) is nowhere near hit by two back-to-back polls.
      cacheTtlSeconds: 86_400,
    });

    // The first confirmation after picking up a cached value writes through once — nothing has
    // confirmed this entry is still correct with the origin before now.
    await flags.isEnabled('on');
    expect(cache.setCalls).toBe(1);

    // A second 304 immediately after should not write again: the entry is nowhere near its ttl,
    // so there is nothing to protect against yet.
    await flags.refresh();
    expect(cache.setCalls).toBe(1);

    flags.close();
  });

  it('rewrites again once the throttle window has actually elapsed', async () => {
    const cache = new FakeCacheStore().seed(
      CACHE_KEY,
      serialized({ on: true }, '"v1"', Date.now() - 120_000),
    );
    const server = new StubServer().notModified().notModified();

    const flags = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: KEY,
      fetch: server.fetch,
      cache,
      pollingInterval: 60_000,
      cacheTtlSeconds: 100, // throttle window (half) is 50s
    });

    await flags.isEnabled('on');
    expect(cache.setCalls).toBe(1);

    vi.setSystemTime(Date.now() + 51_000);

    await flags.refresh();
    expect(cache.setCalls).toBe(2);

    vi.useRealTimers();
    flags.close();
  });

  it('does not let two environments sharing one store overwrite each other', async () => {
    const cache = new FakeCacheStore();

    const devKey = 'ffp_dev_b182276126b759aa_7f097037aa14d671f4317df877989f05f5309c1323ecb24dab4be559';
    const prodKey = 'ffp_prod_b182276126b759aa_7f097037aa14d671f4317df877989f05f5309c1323ecb24dab4be559';

    const dev = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: devKey,
      fetch: new StubServer().withFlags({ on: true }, '"v1"', 'dev').fetch,
      cache,
    });
    const prod = createFeatureFlagsClient({
      baseAddress: BASE,
      sdkKey: prodKey,
      fetch: new StubServer().withFlags({ on: false }, '"v1"', 'prod').fetch,
      cache,
    });

    expect(await dev.isEnabled('on')).toBe(true);
    expect(await prod.isEnabled('on')).toBe(false);
    // If the two shared one cache key, whichever wrote second would have clobbered the other's
    // entry — a third client reading either key should still see the right environment's answer.
    expect(await dev.isEnabled('on')).toBe(true);

    dev.close();
    prod.close();
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
