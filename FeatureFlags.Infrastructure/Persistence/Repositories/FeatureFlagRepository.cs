using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Infrastructure.Persistence.Repositories;

internal sealed class FeatureFlagRepository(AppDbContext dbContext) : IFeatureFlagRepository
{
    public async Task<Option<FeatureFlag>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default)
    {
        var flag = await dbContext.FeatureFlags
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        return flag.ToOption();
    }

    public async Task AddAsync(FeatureFlag flag, CancellationToken cancellationToken = default) =>
        await dbContext.FeatureFlags.AddAsync(flag, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
