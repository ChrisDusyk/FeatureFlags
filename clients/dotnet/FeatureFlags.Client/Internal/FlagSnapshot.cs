using System;
using System.Collections.Generic;

namespace FeatureFlags.Client.Internal;

/// <summary>
/// One version of the answer, whole.
///
/// <para>
/// Immutable, and replaced rather than mutated: a reader takes the current reference and works
/// from it, so a refresh landing mid-read cannot show it half of one version and half of another.
/// That is also what lets reads take no lock at all.
/// </para>
/// </summary>
internal sealed class FlagSnapshot
{
    public FlagSnapshot(
        string environment,
        IReadOnlyDictionary<string, bool> flags,
        string? etag,
        DateTimeOffset fetchedAt)
    {
        Environment = environment;
        Flags = flags;
        ETag = etag;
        FetchedAt = fetchedAt;
    }

    /// <summary>The environment the SDK key is scoped to, as the server reported it.</summary>
    public string Environment { get; }

    public IReadOnlyDictionary<string, bool> Flags { get; }

    /// <summary>What to send as <c>If-None-Match</c> next time, so an unchanged poll costs a 304.</summary>
    public string? ETag { get; }

    public DateTimeOffset FetchedAt { get; }

    /// <summary>The same answer, re-stamped. What a 304 produces.</summary>
    public FlagSnapshot RefreshedAt(DateTimeOffset fetchedAt) =>
        new FlagSnapshot(Environment, Flags, ETag, fetchedAt);
}
