using System.Collections.Generic;

namespace FeatureFlags.Client.Redis.Internal;

/// <summary>
/// What actually crosses the wire to Redis: the same facts as <c>FlagSnapshot</c>, in a type this
/// package owns rather than one shared with the base package. <c>FlagSnapshot</c> is internal to
/// <c>FeatureFlags.Client</c> for a reason unrelated to this — its constructor being reachable here
/// is not the same thing as it being a stable serialization contract, and this cache's wire format
/// should not move just because that type's shape does.
///
/// <para>
/// The sole constructor is what System.Text.Json binds to on deserialization; no
/// <c>[JsonConstructor]</c> is needed to say so because there is only one to pick.
/// </para>
/// </summary>
internal sealed class CachedFlags(string environment, IReadOnlyDictionary<string, bool> flags, string? etag)
{
    public string Environment { get; } = environment;

    public IReadOnlyDictionary<string, bool> Flags { get; } = flags;

    public string? ETag { get; } = etag;
}
