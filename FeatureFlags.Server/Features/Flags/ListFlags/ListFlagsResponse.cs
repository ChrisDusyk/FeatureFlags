using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;

namespace FeatureFlags.Server.Features.Flags.ListFlags;

/// <summary>
/// A flag as one environment sees it. <see cref="IsEnabled"/> and <see cref="UpdatedAt"/> come from
/// that environment's state, so switching environments genuinely changes the answer.
/// </summary>
public sealed record FlagSummary(
    Guid Id,
    string Key,
    string Name,
    string Description,
    bool IsEnabled,
    DateTimeOffset UpdatedAt)
{
    public static FlagSummary From(FeatureFlag flag, EnvironmentKey environment) =>
        flag.StateIn(environment).Match(
            state => new FlagSummary(
                flag.Id,
                flag.Key.Value,
                flag.Name,
                flag.Description,
                state.IsEnabled,
                state.UpdatedAt),
            // A flag with no state for an environment cannot happen while the set is fixed. Report
            // it as off and last touched when the flag was, rather than dropping the row: a flag
            // vanishing from the list is a worse lie than a flag shown off.
            () => new FlagSummary(
                flag.Id,
                flag.Key.Value,
                flag.Name,
                flag.Description,
                false,
                flag.UpdatedAt));
}

public sealed record ListFlagsResponse(string Environment, IReadOnlyList<FlagSummary> Flags);
