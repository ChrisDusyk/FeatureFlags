using FeatureFlags.Domain.Flags;

namespace FeatureFlags.Server.Features.Flags.CreateFlag;

public sealed record CreateFlagResponse(
    Guid Id,
    string Key,
    string Name,
    string Description,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static CreateFlagResponse From(FeatureFlag flag) => new(
        flag.Id,
        flag.Key.Value,
        flag.Name,
        flag.Description,
        flag.IsEnabled,
        flag.CreatedAt,
        flag.UpdatedAt);
}
