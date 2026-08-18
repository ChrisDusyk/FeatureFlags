namespace FeatureFlags.Domain.Flags.Events;

/// <summary>
/// Something that happened to a flag. The sequence number that orders these within a flag's
/// stream is assigned when an event is appended, not carried on the event itself.
/// </summary>
public interface IFlagEvent
{
    Guid FlagId { get; }
    DateTimeOffset OccurredAt { get; }
}
