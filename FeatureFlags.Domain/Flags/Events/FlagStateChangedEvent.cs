using FeatureFlags.Domain.Environments;

namespace FeatureFlags.Domain.Flags.Events;

public sealed record FlagStateChangedEvent(
    Guid FlagId,
    EnvironmentKey Environment,
    bool IsEnabled,
    DateTimeOffset OccurredAt) : IFlagEvent;
