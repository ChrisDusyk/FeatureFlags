using FeatureFlags.Domain.Environments;

namespace FeatureFlags.Server.Features.Flags.ListFlags;

/// <summary>
/// Every flag, answered for one environment. The environment is not optional: a list that quietly
/// picked one for you would report an on-or-off the caller never asked about.
/// </summary>
public sealed record ListFlagsQuery(EnvironmentKey Environment);
