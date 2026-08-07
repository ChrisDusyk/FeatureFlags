using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlags.Client;

/// <summary>
/// Reads feature flags for the environment this client's SDK key is scoped to.
///
/// <para>
/// Registered as a singleton by <c>AddFeatureFlags</c>. Reads are served from an in-memory
/// snapshot, so calling <see cref="IsEnabledAsync(string, CancellationToken)"/> on a hot path costs
/// a dictionary lookup rather than a request.
/// </para>
/// </summary>
public interface IFeatureFlagClient
{
    /// <summary>
    /// Whether a flag is on. A key this installation has never heard of is <c>false</c>: a flag
    /// that does not exist is not one that is on.
    /// </summary>
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a flag is on, with the answer to give when there is nothing to go on — an unknown
    /// key, or a snapshot that has never loaded because the service could not be reached.
    /// </summary>
    Task<bool> IsEnabledAsync(string key, bool defaultValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refetches now, rather than waiting for the polling interval. Throws if the fetch fails —
    /// unlike the background refresh, an explicit request to reload reports what happened.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
