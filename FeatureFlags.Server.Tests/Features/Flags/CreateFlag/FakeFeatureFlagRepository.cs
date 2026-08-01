using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Tests.Features.Flags.CreateFlag;

/// <summary>
/// In-memory stand-in for the EF repository. Tracks added flags and save calls so tests can
/// assert the handler persists exactly once and only on the success path.
/// </summary>
internal sealed class FakeFeatureFlagRepository : IFeatureFlagRepository
{
    private readonly Dictionary<FlagKey, FeatureFlag> _committed = [];
    private readonly List<FeatureFlag> _pending = [];

    public int SaveChangesCallCount { get; private set; }

    public IReadOnlyCollection<FeatureFlag> Committed => _committed.Values;

    public void Seed(FeatureFlag flag) => _committed[flag.Key] = flag;

    public Task<Option<FeatureFlag>> GetByKeyAsync(FlagKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_committed.TryGetValue(key, out var flag)
            ? Option<FeatureFlag>.Some(flag)
            : Option<FeatureFlag>.None);

    public Task AddAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        _pending.Add(flag);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        foreach (var flag in _pending)
            _committed[flag.Key] = flag;

        _pending.Clear();

        return Task.CompletedTask;
    }
}
