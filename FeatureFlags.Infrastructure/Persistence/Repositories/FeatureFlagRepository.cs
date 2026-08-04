using System.Diagnostics.CodeAnalysis;
using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FeatureFlags.Infrastructure.Persistence.Repositories;

internal sealed class FeatureFlagRepository(AppDbContext dbContext) : IFeatureFlagRepository
{
    public async Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default) =>
        // Owned collections come along without an Include, so every flag arrives with its states.
        await dbContext.FeatureFlags
            .OrderBy(flag => flag.Key)
            .ToListAsync(cancellationToken);

    public async Task<Option<FeatureFlag>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default)
    {
        var flag = await dbContext.FeatureFlags
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        return flag.ToOption();
    }

    public async Task AddAsync(FeatureFlag flag, CancellationToken cancellationToken = default) =>
        await dbContext.FeatureFlags.AddAsync(flag, cancellationToken);

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException exception) when (TryGetDuplicateKey(exception, out var duplicateKey))
        {
            // Another writer took this key between the caller's check and this insert. The unique
            // index is what actually settles the race; translating it keeps the outcome a Conflict
            // rather than an unhandled exception.
            dbContext.ChangeTracker.Clear();

            return Result.Failure(FlagErrors.DuplicateKey(duplicateKey));
        }
    }

    private static bool TryGetDuplicateKey(DbUpdateException exception, [NotNullWhen(true)] out FlagKey? key)
    {
        key = exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: FeatureFlagConfiguration.KeyIndexName
        }
            ? exception.Entries
                .Select(entry => entry.Entity)
                .OfType<FeatureFlag>()
                .FirstOrDefault()?.Key
            : null;

        return key is not null;
    }
}
