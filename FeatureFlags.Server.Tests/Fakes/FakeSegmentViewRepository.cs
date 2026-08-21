using FeatureFlags.Domain.Segments;
using FeatureFlags.Domain.Segments.Events;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Tests.Fakes;

/// <summary>
/// In-memory stand-in for the segment read side. Like the real one, everything here except
/// <see cref="GetHistoryAsync"/> hides retired segments — a test that seeds one and then expects to
/// find it is asserting the wrong thing.
/// </summary>
internal sealed class FakeSegmentViewRepository : ISegmentViewRepository
{
    private readonly Dictionary<SegmentKey, SegmentView> _views = [];
    private readonly Dictionary<Guid, List<ISegmentEvent>> _histories = [];

    public void Seed(SegmentView view) => _views[view.Key] = view;

    /// <summary>Seeds a segment by key alone, for the callers that only ever ask whether it exists.</summary>
    public void Seed(SegmentKey key) => Seed(new SegmentView(
        Guid.CreateVersion7(), key, key.Value, string.Empty, SegmentDefinition.Empty,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));

    /// <summary>Sets the events <see cref="GetHistoryAsync"/> returns for a segment id, newest
    /// first — matching the real repository's <c>ORDER BY SequenceNumber DESC</c>.</summary>
    public void SeedHistory(Guid segmentId, params ISegmentEvent[] events) => _histories[segmentId] = [.. events];

    public Task<IReadOnlyList<SegmentView>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SegmentView>>(
            [.. _views.Values.OrderBy(view => view.Key.Value, StringComparer.Ordinal)]);

    public Task<Option<SegmentView>> GetByKeyAsync(SegmentKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_views.TryGetValue(key, out var view)
            ? Option<SegmentView>.Some(view)
            : Option<SegmentView>.None);

    public Task<IReadOnlyList<SegmentKey>> FilterExistingAsync(
        IReadOnlyCollection<SegmentKey> keys,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SegmentKey>>([.. keys.Distinct().Where(_views.ContainsKey)]);

    public Task<IReadOnlyList<ISegmentEvent>> GetHistoryAsync(
        Guid segmentId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ISegmentEvent>>(
            _histories.TryGetValue(segmentId, out var events) ? events : []);
}
