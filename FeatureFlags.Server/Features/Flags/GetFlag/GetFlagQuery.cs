using FeatureFlags.Domain.Flags;

namespace FeatureFlags.Server.Features.Flags.GetFlag;

public sealed record GetFlagQuery(FlagKey Key);
