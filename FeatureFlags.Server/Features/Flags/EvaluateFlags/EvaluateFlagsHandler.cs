using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;
using Microsoft.Extensions.Caching.Hybrid;

namespace FeatureFlags.Server.Features.Flags.EvaluateFlags;

/// <summary>
/// The read path a fleet of application servers sits on, so it is the one place here that caches.
///
/// <para>
/// Two things keep it cheap. The <see cref="EvaluatedFlags.ETag"/> means a poll that finds nothing
/// changed costs a 304 and no body — which is most polls, because flags change rarely and are
/// asked about constantly. The cache means the polls that do get a body still mostly do not reach
/// Postgres: <see cref="HybridCache"/> answers from memory first and Redis second, and collapses a
/// stampede of simultaneous misses into one query.
/// </para>
///
/// <para>
/// The cost is <see cref="CacheLifetime"/> of staleness after a toggle. That is the honest contract
/// for a client that polls anyway — an SDK's own interval dwarfs it — and it is why the console
/// reads the admin listing instead of this, so what an operator sees after flipping a switch is
/// always the truth.
/// </para>
/// </summary>
public sealed class EvaluateFlagsHandler(IFlagViewRepository repository, HybridCache cache)
{
    /// <summary>
    /// Short enough that nobody reasons about it, long enough to absorb a fleet's poll. Invalidating
    /// on write instead would be tighter, and would put a cache concern into every slice that
    /// changes a flag; this stays in the one slice that reads.
    /// </summary>
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);

    public async Task<Result<EvaluatedFlags>> HandleAsync(
        EvaluateFlagsQuery query,
        CancellationToken cancellationToken = default)
    {
        var evaluated = await cache.GetOrCreateAsync(
            CacheKeyFor(query.Environment),
            (Repository: repository, query.Environment),
            static async (state, token) =>
            {
                var flags = await state.Repository.ListAsync(token);

                return Evaluate(flags, state.Environment);
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheLifetime,
                LocalCacheExpiration = CacheLifetime
            },
            cancellationToken: cancellationToken);

        return Result.Success(evaluated);
    }

    private static string CacheKeyFor(EnvironmentKey environment) => $"evaluation:{environment.Value}";

    /// <summary>
    /// Builds the answer and the ETag together, from the same ordered pass, so the tag cannot
    /// describe a payload the caller did not get.
    /// </summary>
    internal static EvaluatedFlags Evaluate(IReadOnlyList<FlagView> flags, EnvironmentKey environment)
    {
        // Ordered explicitly rather than relying on the repository's ordering: the ETag is only
        // meaningful if the same set of states always hashes the same way.
        var states = flags
            .Select(flag => (Key: flag.Key.Value, flag.IsEnabledIn(environment)))
            .OrderBy(state => state.Key, StringComparer.Ordinal)
            .ToList();

        // The environment leads the fingerprint because it is in the body: two environments that
        // happen to agree on every flag are still two different responses, and one ETag standing
        // for both would let a client be told nothing had changed when everything had.
        var fingerprint = new StringBuilder(environment.Value).Append('\n');
        var evaluated = new Dictionary<string, bool>(states.Count, StringComparer.Ordinal);

        foreach (var (key, isEnabled) in states)
        {
            evaluated[key] = isEnabled;
            fingerprint.Append(key).Append(isEnabled ? "=1\n" : "=0\n");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.ToString()));

        return new EvaluatedFlags(
            new EvaluateFlagsResponse(environment.Value, evaluated),
            // Quoted because an ETag is a quoted-string on the wire, and a bare one is silently
            // ignored by anything that reads the header properly.
            $"\"{Base64Url.EncodeToString(hash)}\"");
    }
}

/// <summary>The response and the tag that identifies this exact version of it.</summary>
public sealed record EvaluatedFlags(EvaluateFlagsResponse Response, string ETag);
