using FeatureFlags.Domain.Flags.Events;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Flags;

/// <summary>Read-only access to the projected current state of every flag. See <see cref="FlagView"/>.</summary>
public interface IFlagViewRepository
{
    /// <summary>Every flag, ordered by key. Each carries its state for all environments.</summary>
    Task<IReadOnlyList<FlagView>> ListAsync(CancellationToken cancellationToken = default);

    Task<Option<FlagView>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default);

    /// <summary>One flag's full event history, newest first. Small and unpaginated — a flag's
    /// event count stays in the dozens even over a long life. Empty, not a failure, for an
    /// unknown key — the caller answers "does this flag exist" via <see cref="GetByKeyAsync"/>.</summary>
    Task<IReadOnlyList<IFlagEvent>> GetHistoryAsync(FlagKey key, CancellationToken cancellationToken = default);
}
