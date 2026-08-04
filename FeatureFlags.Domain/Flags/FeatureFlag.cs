using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Flags;

/// <summary>
/// A single toggleable feature, identified by its <see cref="FlagKey"/>.
/// <para>
/// A flag's identity is global — one key, one name, one description, everywhere. Whether it is
/// <em>on</em> is answered once per environment by a <see cref="FlagState"/>, so a feature can be
/// live in development and dark in production while still being the same flag.
/// </para>
/// Timestamps are supplied by the caller rather than read from a clock so the entity stays deterministic.
/// </summary>
public sealed class FeatureFlag
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 1000;

    private readonly List<FlagState> _states = [];

    private FeatureFlag(
        Guid id,
        FlagKey key,
        string name,
        string description,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
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
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// When the flag itself last changed — its name or description. Toggling an environment moves
    /// that <see cref="FlagState.UpdatedAt"/>, not this one.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>One state per environment in <see cref="EnvironmentKey.All"/>, always.</summary>
    public IReadOnlyCollection<FlagState> States => _states;

    /// <summary>
    /// Creates a flag in every environment at once. It is on only where <paramref name="enabledIn"/>
    /// says so and off everywhere else — a new flag reaching production unasked is not a default
    /// worth having.
    /// </summary>
    public static Result<FeatureFlag> Create(
        string? key,
        string? name,
        string? description,
        IEnumerable<EnvironmentKey> enabledIn,
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

        var enabled = enabledIn.ToHashSet();

        var flag = new FeatureFlag(
            Guid.CreateVersion7(),
            keyResult.Value,
            trimmedName,
            trimmedDescription,
            timestamp,
            timestamp);

        foreach (var environment in EnvironmentKey.All)
            flag._states.Add(new FlagState(environment, enabled.Contains(environment), timestamp));

        return Result.Success(flag);
    }

    public bool IsEnabledIn(EnvironmentKey environment) =>
        StateIn(environment).Match(state => state.IsEnabled, () => false);

    /// <summary>
    /// The state for one environment. <see cref="Option{T}.None"/> only when a flag predates an
    /// environment that was added after it — which cannot happen while the set is fixed, but the
    /// caller still has to answer for it rather than being handed a null.
    /// </summary>
    public Option<FlagState> StateIn(EnvironmentKey environment) =>
        _states.FirstOrDefault(state => state.Environment == environment).ToOption();

    /// <summary>
    /// Turns the flag on or off in one environment. Idempotent — setting the state it is already in
    /// leaves both the state and its timestamp untouched.
    /// </summary>
    public Result SetEnabled(EnvironmentKey environment, bool isEnabled, DateTimeOffset timestamp) =>
        StateIn(environment).Match(
            state =>
            {
                state.SetEnabled(isEnabled, timestamp);
                return Result.Success();
            },
            () => Result.Failure(FlagErrors.StateMissing(Key, environment)));
}
