# FeatureFlags.Client

Reads feature flags from a self-hosted [FeatureFlags](https://github.com/ChrisDusyk/FeatureFlags)
installation.

```sh
dotnet add package FeatureFlags.Client
```

Targets `netstandard2.0`, `net8.0`, and `net10.0` — so it works on .NET Framework 4.6.2 and up as
well as modern .NET.

## Use

```csharp
builder.Services.AddFeatureFlags(options =>
{
    options.BaseAddress = new Uri("https://flags.example.com");
    options.SdkKey = builder.Configuration["FeatureFlags:SdkKey"];
});
```

or bind a configuration section:

```csharp
builder.Services.AddFeatureFlags(builder.Configuration.GetSection("FeatureFlags"));
```

```csharp
public sealed class CheckoutService(IFeatureFlagClient flags)
{
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        if (await flags.IsEnabledAsync("new-checkout", cancellationToken))
        {
            // ...
        }
    }
}
```

Issue an SDK key in the console under **Organization → Environments**. It is shown once.

**There is no environment setting.** A key is issued for one environment and carries it, so the
server decides which flags you see. One thing to configure, and no way for it to disagree with what
the console shows.

## How it behaves

**Reads do not make requests.** `IsEnabledAsync` answers from an in-memory snapshot — a dictionary
lookup, safe to call on a hot path. The snapshot is refreshed in the background every
`PollingInterval` (30 seconds by default), and lazily on read if it has gone stale, which is what
makes the package work outside a generic host.

**A poll that finds nothing changed is a 304 with no body.** The client sends the previous `ETag`
back as `If-None-Match`.

**An unreachable server does not throw at your callers.** `IsEnabledAsync` falls back to the last
snapshot it managed to read, and to your default if it never read one. A flag service being briefly
unavailable should not take down everything that reads it. Set `ThrowOnStartupFailure` if starting
blind is worse for you than not starting, and call `RefreshAsync` when you want a failure reported.

**An unknown key is `false`** — a flag that does not exist is not one that is on. Use the
`defaultValue` overload to say otherwise.

## Options

| | | |
|---|---|---|
| `BaseAddress` | — | The origin the console is on. Required. |
| `SdkKey` | — | Issued in the console. Required. |
| `PollingInterval` | 30s | Upper bound on how long a toggle takes to arrive. |
| `Timeout` | 10s | How long one refresh may take. |
| `ThrowOnStartupFailure` | `false` | Whether an unreadable first snapshot stops the host. |

## Versioning

This package versions independently of the platform. Its compatibility surface is
`GET /api/evaluation` and the SDK key format, not the admin API — which is closed to SDK keys by
design.
