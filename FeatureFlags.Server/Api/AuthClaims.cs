using System.Security.Claims;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Api;

/// <summary>
/// Reads the claims the auth service puts in a token. Inbound claim mapping is turned off (see
/// <see cref="AuthenticationExtensions"/>), so these are the JWT's own names rather than the
/// long-form WS-Federation URIs ASP.NET would otherwise substitute.
/// </summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";

    /// <summary>
    /// The signed-in user's id, or <c>None</c> when the token carries no usable subject —
    /// which should not happen, but is a claim from outside and so is not assumed.
    /// </summary>
    public static Option<Guid> GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(Subject), out var id)
            ? Option<Guid>.Some(id)
            : Option<Guid>.None;
}
