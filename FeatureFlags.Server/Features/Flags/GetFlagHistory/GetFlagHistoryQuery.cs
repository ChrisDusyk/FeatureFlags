using FeatureFlags.Domain.Flags;

namespace FeatureFlags.Server.Features.Flags.GetFlagHistory;

public sealed record GetFlagHistoryQuery(FlagKey Key);
