using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.SdkKeys;

public static class SdkKeyErrors
{
    public static Error NameRequired => Error.Validation(
        "SdkKey.Name.Required",
        "An SDK key needs a name, so it can be told apart from the others when one has to be revoked.");

    public static Error NameTooLong => Error.Validation(
        "SdkKey.Name.TooLong",
        $"An SDK key name must be {SdkKey.MaxNameLength} characters or fewer.");

    public static Error KindRequired => Error.Validation(
        "SdkKey.Kind.Required",
        $"An SDK key needs a kind: {string.Join(" or ", SdkKeyKind.All.Select(kind => $"'{kind}'"))}.");

    public static Error KindUnrecognized(string value) => Error.Validation(
        "SdkKey.Kind.Unrecognized",
        $"'{value}' is not an SDK key kind. Use {string.Join(" or ", SdkKeyKind.All.Select(kind => $"'{kind}'"))}.");

    /// <summary>
    /// A secret key presented from a browser. The request carried an <c>Origin</c> header, which
    /// only a browser sends, and a secret shipped to a browser is a secret no longer.
    ///
    /// <para>
    /// Unlike the failures below this is deliberately explicit rather than uniform: the caller
    /// already holds this credential, so nothing is being disclosed, and someone who has just wired
    /// the wrong key into a web application needs to be told which mistake they made. A bare 401
    /// would send them looking for a typo.
    /// </para>
    /// </summary>
    public static Error SecretKeyFromBrowser => Error.Unauthorized(
        "SdkKey.SecretFromBrowser",
        "This is a secret SDK key, and the request came from a browser. Anything shipped to a " +
        "browser is readable by everyone who receives it. Issue a publishable key for this " +
        "application instead.");

    public static Error NotFound(Guid id) => Error.NotFound(
        "SdkKey.NotFound",
        $"No SDK key with the id '{id}' exists.");

    public static Error AlreadyRevoked => Error.Conflict(
        "SdkKey.AlreadyRevoked",
        "This SDK key has already been revoked.");

    /// <summary>
    /// Every way a presented token can be wrong — wrong shape, unknown selector, wrong secret,
    /// revoked — answers with this one error. A caller holding a bad credential is told it is bad;
    /// which kind of bad is information about a key it does not have.
    /// </summary>
    public static Error TokenMalformed => Error.Unauthorized(
        "SdkKey.Invalid",
        "The SDK key is not valid.");
}
