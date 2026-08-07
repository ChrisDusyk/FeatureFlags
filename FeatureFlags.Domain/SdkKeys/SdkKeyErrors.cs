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
