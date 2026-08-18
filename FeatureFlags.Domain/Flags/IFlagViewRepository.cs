using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Flags;

/// <summary>Read-only access to the projected current state of every flag. See <see cref="FlagView"/>.</summary>
public interface IFlagViewRepository
{
    /// <summary>Every flag, ordered by key. Each carries its state for all environments.</summary>
    Task<IReadOnlyList<FlagView>> ListAsync(CancellationToken cancellationToken = default);

    Task<Option<FlagView>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default);
}
