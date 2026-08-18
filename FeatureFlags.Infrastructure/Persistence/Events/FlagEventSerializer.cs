using System.Text.Json;
using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Flags.Events;

namespace FeatureFlags.Infrastructure.Persistence.Events;

/// <summary>
/// Converts between the domain's <see cref="IFlagEvent"/> types and their persisted
/// <see cref="FlagEventRecord"/> shape. The domain events carry no serialization concern of their
/// own — this is the one place that knows the discriminator strings and payload shapes, including
/// the ones the <c>AddFlagEvents</c> migration's backfill SQL writes directly.
/// </summary>
internal static class FlagEventSerializer
{
    internal const string FlagCreatedEventType = "FlagCreated";
    internal const string FlagStateChangedEventType = "FlagStateChanged";

    // Case-insensitive so the migration's hand-written backfill JSON doesn't have to match this
    // type's property casing exactly.
    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    internal static FlagEventRecord ToRecord(Guid flagId, int sequenceNumber, IFlagEvent @event) => @event switch
    {
        FlagCreatedEvent created => new FlagEventRecord
        {
            FlagId = flagId,
            SequenceNumber = sequenceNumber,
            EventType = FlagCreatedEventType,
            Payload = JsonSerializer.Serialize(
                new FlagCreatedPayload(created.Key.Value, created.Name, created.Description), PayloadOptions),
            OccurredAt = created.OccurredAt,
        },
        FlagStateChangedEvent stateChanged => new FlagEventRecord
        {
            FlagId = flagId,
            SequenceNumber = sequenceNumber,
            EventType = FlagStateChangedEventType,
            Payload = JsonSerializer.Serialize(
                new FlagStateChangedPayload(stateChanged.Environment.Value, stateChanged.IsEnabled), PayloadOptions),
            OccurredAt = stateChanged.OccurredAt,
        },
        _ => throw new InvalidOperationException($"Unrecognized flag event type '{@event.GetType()}'."),
    };

    internal static IFlagEvent ToEvent(FlagEventRecord record) => record.EventType switch
    {
        FlagCreatedEventType => ToFlagCreatedEvent(record),
        FlagStateChangedEventType => ToFlagStateChangedEvent(record),
        _ => throw new InvalidOperationException($"Unrecognized flag event type '{record.EventType}' on flag {record.FlagId}."),
    };

    private static FlagCreatedEvent ToFlagCreatedEvent(FlagEventRecord record)
    {
        var payload = JsonSerializer.Deserialize<FlagCreatedPayload>(record.Payload, PayloadOptions)!;
        return new FlagCreatedEvent(record.FlagId, FlagKey.FromPersisted(payload.Key), payload.Name, payload.Description, record.OccurredAt);
    }

    private static FlagStateChangedEvent ToFlagStateChangedEvent(FlagEventRecord record)
    {
        var payload = JsonSerializer.Deserialize<FlagStateChangedPayload>(record.Payload, PayloadOptions)!;
        return new FlagStateChangedEvent(record.FlagId, EnvironmentKey.FromPersisted(payload.Environment), payload.IsEnabled, record.OccurredAt);
    }

    private sealed record FlagCreatedPayload(string Key, string Name, string Description);

    private sealed record FlagStateChangedPayload(string Environment, bool IsEnabled);
}
