using FeatureFlags.Domain.SdkKeys;

namespace FeatureFlags.Server.Features.SdkKeys.IssueSdkKey;

/// <summary>What the caller sends. The environment arrives as its key, e.g. <c>"dev"</c>.</summary>
public sealed record IssueSdkKeyRequest(string? Name, string? Environment);

/// <summary>
/// The one and only response that carries <see cref="Token"/>. Every other read of an SDK key
/// answers with <c>SdkKeySummary</c>, which cannot.
/// </summary>
public sealed record IssueSdkKeyResponse(
    Guid Id,
    string Name,
    string Environment,
    string Token,
    DateTimeOffset CreatedAt)
{
    public static IssueSdkKeyResponse From(IssuedSdkKey issued) => new(
        issued.Key.Id,
        issued.Key.Name,
        issued.Key.Environment.Value,
        issued.Token,
        issued.Key.CreatedAt);
}
