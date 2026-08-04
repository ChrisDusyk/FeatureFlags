using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;

namespace FeatureFlags.Server.Features.Flags.ToggleFlag;

public sealed record ToggleFlagResponse(
    string Key,
    string Environment,
    bool IsEnabled,
    DateTimeOffset UpdatedAt)
{
    public static ToggleFlagResponse From(FeatureFlag flag, EnvironmentKey environment) =>
        flag.StateIn(environment).Match(
            state => new ToggleFlagResponse(flag.Key.Value, environment.Value, state.IsEnabled, state.UpdatedAt),
            () => new ToggleFlagResponse(flag.Key.Value, environment.Value, false, flag.UpdatedAt));
}
