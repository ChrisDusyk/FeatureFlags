using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Flags.Events;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Domain.Users;

namespace FeatureFlags.Server.Features.Flags.GetFlagHistory;

public sealed class GetFlagHistoryHandler(IFlagViewRepository viewRepository, IUserRepository userRepository)
{
    public async Task<Result<GetFlagHistoryResponse>> HandleAsync(
        GetFlagHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var flagResult = (await viewRepository.GetByKeyAsync(query.Key, cancellationToken))
            .ToResult(FlagErrors.NotFound(query.Key));

        if (flagResult.IsFailure)
            return Result.Failure<GetFlagHistoryResponse>(flagResult.Error);

        var events = await viewRepository.GetHistoryAsync(query.Key, cancellationToken);

        // Resolved once per distinct actor rather than once per event, so a flag with twenty
        // toggles by the same person costs one lookup, not twenty.
        var causedByIds = events.Select(@event => @event.CausedBy).Where(id => id is not null).Select(id => id!.Value).Distinct();

        var names = new Dictionary<Guid, string>();
        foreach (var userId in causedByIds)
        {
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            var name = user.Match(found => found.Name is { Length: > 0 } ? found.Name : found.Email, () => (string?)null);

            if (name is not null)
                names[userId] = name;
        }

        var entries = events.Select(@event => ToEntry(@event, names)).ToList();

        return Result.Success(new GetFlagHistoryResponse(entries));
    }

    private static FlagHistoryEntryResponse ToEntry(IFlagEvent @event, IReadOnlyDictionary<Guid, string> names)
    {
        var causedByName = @event.CausedBy is { } id && names.TryGetValue(id, out var name) ? name : null;

        return @event switch
        {
            FlagCreatedEvent created => new FlagHistoryEntryResponse(
                "FlagCreated", created.OccurredAt, causedByName, created.Name, created.Description, null, null),
            FlagDetailsChangedEvent detailsChanged => new FlagHistoryEntryResponse(
                "FlagDetailsChanged", detailsChanged.OccurredAt, causedByName, detailsChanged.Name, detailsChanged.Description, null, null),
            FlagStateChangedEvent stateChanged => new FlagHistoryEntryResponse(
                "FlagStateChanged", stateChanged.OccurredAt, causedByName, null, null, stateChanged.Environment.Value, stateChanged.IsEnabled),
            _ => throw new InvalidOperationException($"Unrecognized flag event type '{@event.GetType()}'."),
        };
    }
}
