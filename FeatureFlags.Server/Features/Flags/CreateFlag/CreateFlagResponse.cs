using FeatureFlags.Domain.Flags;

namespace FeatureFlags.Server.Features.Flags.CreateFlag;

public sealed record FlagStateResponse(string Environment, bool IsEnabled, DateTimeOffset UpdatedAt);

public sealed record CreateFlagResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<FlagStateResponse> States)
{
    public static CreateFlagResponse From(FeatureFlag flag) => new(
        flag.Id,
        flag.Key.Value,
        flag.Name,
        flag.Description,
        flag.CreatedAt,
        flag.UpdatedAt,
        [.. flag.States.Select(state =>
            new FlagStateResponse(state.Environment.Value, state.IsEnabled, state.UpdatedAt))]);
}
