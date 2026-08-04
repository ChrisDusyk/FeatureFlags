namespace FeatureFlags.Server.Api;

/// <summary>
/// The authorization policies every slice draws on. There are only two, matching the two roles:
/// <see cref="SignedIn"/> is the ordinary console, <see cref="Admin"/> is managing the
/// organization's members.
/// </summary>
public static class AuthPolicies
{
    public const string SignedIn = "signed-in";
    public const string Admin = "admin";
}
