using FeatureFlags.Domain.Environments;

namespace FeatureFlags.Domain.Flags;

/// <summary>Whether a flag is on in one environment, as currently projected. Derived data, not
/// validated input — unlike <see cref="FlagState"/> there is nothing here to protect.</summary>
public sealed record FlagStateView(EnvironmentKey Environment, bool IsEnabled, DateTimeOffset UpdatedAt);
