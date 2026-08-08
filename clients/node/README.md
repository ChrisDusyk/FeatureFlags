# @featureflags/client

Reads feature flags from a self-hosted [FeatureFlags](https://github.com/ChrisDusyk/FeatureFlags)
installation. Runs on a server or in a browser.

```sh
pnpm add @featureflags/client
```

ESM only. Node 20.19+ / 22.12+, and any browser with `fetch`.

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

## Options

| | | |
|---|---|---|
| `baseAddress` | — | The origin the console is on. Required. |
| `sdkKey` | — | Issued in the console. Required. |
| `pollingInterval` | `30000` | Upper bound, in ms, on how long a toggle takes to arrive. |
| `timeout` | `10000` | How long one refresh may take, in ms. |
| `fetch` | global | For tests, or a proxy agent. |

## Versioning

This package versions independently of the platform. Its compatibility surface is
`GET /api/evaluation` and the SDK key format, not the admin API — which is closed to SDK keys by
design.
