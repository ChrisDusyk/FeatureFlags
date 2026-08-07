using System;

namespace FeatureFlags.Client;

/// <summary>
/// How to reach a FeatureFlags installation.
///
/// <para>
/// There is deliberately no environment here. An SDK key is issued for one environment and carries
/// it, so the server decides which flags this client sees — one thing to configure, and no way for
/// it to drift from what the console shows.
/// </para>
/// </summary>
public sealed class FeatureFlagsOptions
{
    /// <summary>
    /// The origin the console is on — <c>https://flags.example.com</c>. The same value the
    /// installation was deployed with as <c>FEATUREFLAGS_ORIGIN</c>.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// A key issued under Organization → Environments. It is shown once, when it is issued.
    /// </summary>
    public string? SdkKey { get; set; }

    /// <summary>
    /// How stale an answer may get before it is refetched. This is the upper bound on how long a
    /// toggle takes to reach this process, and the lower bound on how often it asks — a poll that
    /// finds nothing changed costs a 304 with no body, so this can be shorter than it looks.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long a single refresh may take before it is abandoned.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether a failure to load the first snapshot should stop the application from starting.
    ///
    /// <para>
    /// Off by default, because the alternative is that a flag service being briefly unreachable
    /// takes down everything that reads it. Left off, an application starts on its defaults and
    /// picks up the real answers when the service answers. Turn it on if starting blind is worse
    /// for you than not starting.
    /// </para>
    /// </summary>
    public bool ThrowOnStartupFailure { get; set; }
}
