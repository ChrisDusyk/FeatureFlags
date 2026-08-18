using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Flags.Events;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Infrastructure.Persistence.Events;
using FeatureFlags.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Infrastructure.Persistence.Repositories;

/// <summary>The read side — a pure projection query, no event replay.</summary>
internal sealed class FlagViewRepository(AppDbContext dbContext) : IFlagViewRepository
{
    public async Task<IReadOnlyList<FlagView>> ListAsync(CancellationToken cancellationToken = default) =>
        [.. (await dbContext.FlagRows.OrderBy(row => row.Key).ToListAsync(cancellationToken)).Select(ToView)];

    public async Task<Option<FlagView>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.FlagRows
            .FirstOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);

        return row.ToOption().Map(ToView);
    }

    public async Task<IReadOnlyList<IFlagEvent>> GetHistoryAsync(FlagKey key, CancellationToken cancellationToken = default)
    {
        var flagId = await dbContext.FlagRows
            .Where(row => row.Key == key)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (flagId is null)
            return [];

        var records = await dbContext.FlagEvents
            .Where(record => record.FlagId == flagId)
            .OrderByDescending(record => record.SequenceNumber)
            .ToListAsync(cancellationToken);

        return [.. records.Select(FlagEventSerializer.ToEvent)];
    }

    private static FlagView ToView(FlagRow row) => new(
        row.Id,
        row.Key,
        row.Name,
        row.Description,
        row.CreatedAt,
        row.UpdatedAt,
        [.. row.States.Select(state => new FlagStateView(state.Environment, state.IsEnabled, state.UpdatedAt))]);
}
