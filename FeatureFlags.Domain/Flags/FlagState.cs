using FeatureFlags.Domain.Environments;

namespace FeatureFlags.Domain.Flags;

/// <summary>
/// Whether a flag is on in one environment. Owned by its <see cref="FeatureFlag"/> — a state has no
/// meaning apart from the flag it belongs to, which is why only the flag can make or change one.
/// </summary>
public sealed class FlagState
{
    internal FlagState(EnvironmentKey environment, bool isEnabled, DateTimeOffset updatedAt)
    {
        Environment = environment;
        IsEnabled = isEnabled;
        UpdatedAt = updatedAt;
    }

    // EF Core materialization only.
    private FlagState() => Environment = null!;

    public EnvironmentKey Environment { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// When this environment last changed. Deliberately separate from the flag's own
    /// <see cref="FeatureFlag.UpdatedAt"/>: turning a flag on in production says nothing about
    /// development, and a list scoped to one environment should not report otherwise.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Idempotent: setting the state it is already in leaves the timestamp alone too.</summary>
    internal void SetEnabled(bool isEnabled, DateTimeOffset timestamp)
    {
        if (IsEnabled == isEnabled)
            return;

        IsEnabled = isEnabled;
        UpdatedAt = timestamp;
    }
}
