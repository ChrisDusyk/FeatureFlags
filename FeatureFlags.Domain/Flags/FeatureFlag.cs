using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Flags;

/// <summary>
/// A single toggleable feature, identified by its <see cref="FlagKey"/>.
/// Timestamps are supplied by the caller rather than read from a clock so the entity stays deterministic.
/// </summary>
public sealed class FeatureFlag
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 1000;

    private FeatureFlag(
        Guid id,
        FlagKey key,
        string name,
        string description,
        bool isEnabled,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        IsEnabled = isEnabled;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    // EF Core materialization only.
    private FeatureFlag()
    {
        Key = null!;
        Name = null!;
        Description = null!;
    }

    public Guid Id { get; private set; }
    public FlagKey Key { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<FeatureFlag> Create(
        string? key,
        string? name,
        string? description,
        bool isEnabled,
        DateTimeOffset timestamp)
    {
        var keyResult = FlagKey.Create(key);
        if (keyResult.IsFailure)
            return Result.Failure<FeatureFlag>(keyResult.Error);

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<FeatureFlag>(FlagErrors.NameRequired);

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
            return Result.Failure<FeatureFlag>(FlagErrors.NameTooLong);

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length > MaxDescriptionLength)
            return Result.Failure<FeatureFlag>(FlagErrors.DescriptionTooLong);

        return Result.Success(new FeatureFlag(
            Guid.CreateVersion7(),
            keyResult.Value,
            trimmedName,
            trimmedDescription,
            isEnabled,
            timestamp,
            timestamp));
    }

    /// <summary>Turns the flag on. Idempotent — re-enabling an enabled flag leaves it untouched.</summary>
    public void Enable(DateTimeOffset timestamp) => SetEnabled(true, timestamp);

    /// <summary>Turns the flag off. Idempotent — re-disabling a disabled flag leaves it untouched.</summary>
    public void Disable(DateTimeOffset timestamp) => SetEnabled(false, timestamp);

    private void SetEnabled(bool isEnabled, DateTimeOffset timestamp)
    {
        if (IsEnabled == isEnabled)
            return;

        IsEnabled = isEnabled;
        UpdatedAt = timestamp;
    }
}
