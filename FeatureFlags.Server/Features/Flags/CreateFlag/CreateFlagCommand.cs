namespace FeatureFlags.Server.Features.Flags.CreateFlag;

public sealed record CreateFlagCommand(string? Key, string? Name, string? Description, bool IsEnabled);
