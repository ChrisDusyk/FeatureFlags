# @featureflags/client

Reads feature flags from a self-hosted [FeatureFlags](https://github.com/ChrisDusyk/FeatureFlags)
installation. Runs on a server or in a browser.

```sh
pnpm add @featureflags/client
```

ESM only, published as ES2023. Node 20.19+ / 22.12+, and any browser with `fetch` — the client
builds its request deadlines out of `AbortController` rather than the much newer `AbortSignal.any`,
so the floor stays where `fetch` put it.

## Use

```ts
import { createFeatureFlagsClient } from '@featureflags/client';

const flags = createFeatureFlagsClient({
  baseAddress: 'https://flags.example.com',
  sdkKey: process.env.FEATUREFLAGS_SDK_KEY!,
});

if (await flags.isEnabled('new-checkout')) {
  // ...
}

await flags.isEnabled('dark-mode', true); // default when the flag is unknown
```

Issue a key in the console under **Organization → Environments**. It is shown once.

**There is no environment setting.** A key is issued for one environment and carries it, so the
server decides which flags you see.

## Which key

The console asks where the key will run, and you get one of two kinds:

| | | |
|---|---|---|
| `ffs_…` | **secret** | a backend, a container, a CI job |
| `ffp_…` | **publishable** | a web or mobile app |

**In a browser you need a publishable key.** This client throws immediately if it finds a secret
key in a browser, and the server refuses one on any request carrying an `Origin` header. If you got
that far, treat the key as compromised and revoke it — anything shipped to a browser can be read
out of it.

A publishable key is public by design. Anyone who loads your app can read it, and with it every
flag key in that environment and whether each is on. Name your flags accordingly.

Your app's origin also has to be listed in the installation's `FEATUREFLAGS_BROWSER_ORIGINS`, or
the browser will refuse the response.

## How it behaves

**Reads do not make requests.** `isEnabled` answers from an in-memory snapshot — a map lookup, safe
on a hot path. The snapshot is refreshed on a timer every `pollingInterval` (30 seconds by
default), and on read if it has gone stale.

**A poll that finds nothing changed is a 304 with no body.** The client sends the previous `ETag`
back as `If-None-Match`.

**An unreachable server does not reject at your callers.** `isEnabled` falls back to the last
snapshot it managed to read, and to your default if it never read one. A flag service being briefly
unavailable should not take down everything that reads it.

To fail fast at startup instead, await a refresh yourself — that one does report:

```ts
const flags = createFeatureFlagsClient({ ... });
await flags.refresh(); // rejects if the installation cannot be read
```

**The timer never holds a Node process open** (`unref`), so a CLI still exits. Call `close()` to
stop polling explicitly; the client keeps answering from its last snapshot afterwards.

## Surviving a longer outage, or sharing one snapshot across instances

The in-memory snapshot above is lost on restart, and every instance of your application polls the
FeatureFlags server independently. If you'd rather a freshly started instance answer correctly from
its very first read, or want an outage survived for longer than one process happens to stay up,
give the client a `cache` — a small interface you implement against whatever Redis client (or other
store) your own application already uses. Nothing above changes if you don't set one; this is
additive, and there's no default implementation, because there's no Redis client this package could
import without breaking a browser bundle for everyone who never touches this option.

```ts
import Redis from 'ioredis';
import { createFeatureFlagsClient, type FeatureFlagsCacheStore } from '@featureflags/client';

const redis = new Redis(process.env.REDIS_URL!);

const cache: FeatureFlagsCacheStore = {
  get: (key) => redis.get(key),
  set: (key, value, ttlSeconds) => redis.set(key, value, 'EX', ttlSeconds).then(() => {}),
};

const flags = createFeatureFlagsClient({
  baseAddress: 'https://flags.example.com',
  sdkKey: process.env.FEATUREFLAGS_SDK_KEY!,
  cache,
});
```

Or with [`redis`](https://www.npmjs.com/package/redis):

```ts
import { createClient } from 'redis';

const redis = await createClient({ url: process.env.REDIS_URL }).connect();

const cache: FeatureFlagsCacheStore = {
  get: (key) => redis.get(key),
  set: (key, value, ttlSeconds) => redis.set(key, value, { EX: ttlSeconds }).then(() => {}),
};
```

**Two different settings govern staleness, on purpose.** `pollingInterval` is still the normal
freshness bound — how long an answer may go before the origin is asked again. `cacheTtlSeconds`
(86400, a day, by default) is the new one: how long a value written to `cache` may still be served
once the origin is genuinely unreachable. Keep it much larger than `pollingInterval` — if the two
were close, the store would buy almost no protection over the in-memory snapshot alone.

**A cold process reads the store before ever asking the origin**, and if what it holds is still
within `pollingInterval`, that value is trusted outright with no request made at all. An older
value is still handed to the server as the conditional-request baseline, so even a stale-but-
unchanged entry costs only a 304, not a full refetch.

**A failure in your store never surfaces through `isEnabled`.** A blip in your own Redis is not the
FeatureFlags server being unreachable, and is treated as a cache miss, not a client failure.

## Options

| | | |
|---|---|---|
| `baseAddress` | — | The origin the console is on. A path is kept, so an installation served under one works; a credential, query string, or fragment is refused. Required. |
| `sdkKey` | — | Issued in the console. Required. |
| `pollingInterval` | `30000` | Upper bound, in ms, on how long a toggle takes to arrive. |
| `timeout` | `10000` | How long one refresh may take, in ms. |
| `fetch` | global | For tests, or a proxy agent. |
| `cache` | none | A `FeatureFlagsCacheStore` backed by your own Redis (or other store). Optional. |
| `cacheTtlSeconds` | `86400` | How long a value in `cache` survives a real outage. Only meaningful with `cache` set. |
| `cacheKeyPrefix` | `"featureflags:"` | Prefixed onto the key this client uses in `cache`, so it cannot collide with your application's own keys. |

## Versioning

This package versions independently of the platform. Its compatibility surface is
`GET /api/evaluation` and the SDK key format, not the admin API — which is closed to SDK keys by
design.
