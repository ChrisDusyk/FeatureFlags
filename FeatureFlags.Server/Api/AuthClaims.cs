using System.Security.Claims;
using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Api;

/// <summary>
/// Reads the claims a caller's credential carries.
///
/// <para>
/// <see cref="Subject"/>, <see cref="Email"/>, and <see cref="Role"/> come from a user's token, and
/// are the JWT's own names rather than the long-form WS-Federation URIs ASP.NET would otherwise
/// substitute — inbound claim mapping is turned off, see <see cref="AuthenticationExtensions"/>.
/// </para>
/// <para>
/// <see cref="SdkKeyId"/> and <see cref="Environment"/> are minted here, by
/// <see cref="SdkKeyAuthenticationHandler"/>, from a row rather than from a token. The two sets
/// never appear on the same principal: an SDK key has no user behind it and so carries no subject,
/// no email, and no role.
/// </para>
/// </summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";

    public const string SdkKeyId = "sdk_key_id";
    public const string SdkKeyName = "sdk_key_name";

    /// <summary>The environment an SDK key is scoped to. Never present on a user's principal.</summary>
    public const string Environment = "environment";

    /// <summary>
    /// The signed-in user's id, or <c>None</c> when the token carries no usable subject —
    /// which should not happen, but is a claim from outside and so is not assumed.
    /// </summary>
    public static Option<Guid> GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(Subject), out var id)
            ? Option<Guid>.Some(id)
            : Option<Guid>.None;

    /// <summary>
    /// The environment the presented SDK key is scoped to. This is the only place an
    /// SDK-authenticated request gets an environment from — there is no parameter for it, so there
    /// is nothing for a caller to widen.
    /// </summary>
    public static Option<EnvironmentKey> GetSdkKeyEnvironment(this ClaimsPrincipal principal) =>
        EnvironmentKey.Create(principal.FindFirstValue(Environment)).Match(
            Option<EnvironmentKey>.Some,
            _ => Option<EnvironmentKey>.None);
}
