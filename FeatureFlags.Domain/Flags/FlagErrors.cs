using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Flags;

public static class FlagErrors
{
    public static Error KeyRequired => Error.Validation(
        "Flag.Key.Required",
        "A flag key is required.");

    public static Error KeyTooLong => Error.Validation(
        "Flag.Key.TooLong",
        $"A flag key must be {FlagKey.MaxLength} characters or fewer.");

    public static Error KeyInvalidFormat => Error.Validation(
        "Flag.Key.InvalidFormat",
        "A flag key must be a lowercase slug containing only letters, digits, and single hyphens between segments.");

    public static Error NameRequired => Error.Validation(
        "Flag.Name.Required",
        "A flag name is required.");

    public static Error NameTooLong => Error.Validation(
        "Flag.Name.TooLong",
        $"A flag name must be {FeatureFlag.MaxNameLength} characters or fewer.");

    public static Error DescriptionTooLong => Error.Validation(
        "Flag.Description.TooLong",
        $"A flag description must be {FeatureFlag.MaxDescriptionLength} characters or fewer.");

    public static Error DuplicateKey(FlagKey key) => Error.Conflict(
        "Flag.DuplicateKey",
        $"A flag with the key '{key}' already exists.");
}
