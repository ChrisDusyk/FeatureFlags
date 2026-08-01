using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Flags;

public interface IFeatureFlagRepository
{
    Task<Option<FeatureFlag>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default);

    Task AddAsync(FeatureFlag flag, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
