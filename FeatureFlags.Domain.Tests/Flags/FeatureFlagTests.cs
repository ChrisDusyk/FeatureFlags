using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Tests.Flags;

public class FeatureFlagTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static readonly EnvironmentKey[] Nowhere = [];

    [Fact]
    public void Create_WithValidInput_ShouldSucceed()
    {
        var result = FeatureFlag.Create(
            "new-checkout",
            "New checkout",
            "Rolls out the rewritten checkout.",
            [EnvironmentKey.Development],
            Now);

        Assert.True(result.IsSuccess);

        var flag = result.Value;
        Assert.Equal("new-checkout", flag.Key.Value);
        Assert.Equal("New checkout", flag.Name);
        Assert.Equal("Rolls out the rewritten checkout.", flag.Description);
        Assert.Equal(Now, flag.CreatedAt);
        Assert.Equal(Now, flag.UpdatedAt);
        Assert.NotEqual(Guid.Empty, flag.Id);
    }

    [Fact]
    public void Create_ShouldGiveTheFlagAStateInEveryEnvironment()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, Nowhere, Now).Value;

        Assert.Equal(EnvironmentKey.All.Count, flag.States.Count);
        Assert.Equal(
            EnvironmentKey.All,
            [.. flag.States.Select(state => state.Environment)]);
    }

    [Fact]
    public void Create_ShouldEnableOnlyTheEnvironmentsAskedFor()
    {
        var flag = FeatureFlag.Create(
            "new-checkout",
            "New checkout",
            null,
            [EnvironmentKey.Development, EnvironmentKey.Staging],
            Now).Value;

        Assert.True(flag.IsEnabledIn(EnvironmentKey.Development));
        Assert.True(flag.IsEnabledIn(EnvironmentKey.Staging));
        Assert.False(flag.IsEnabledIn(EnvironmentKey.Production));
    }

    [Fact]
    public void Create_WithNoEnvironments_ShouldStartOffEverywhere()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, Nowhere, Now).Value;

        Assert.All(flag.States, state => Assert.False(state.IsEnabled));
    }

    [Fact]
    public void Create_ShouldStampEveryStateWithTheSameTimestamp()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, [EnvironmentKey.Production], Now).Value;

        Assert.All(flag.States, state => Assert.Equal(Now, state.UpdatedAt));
    }

    [Fact]
    public void Create_WithARepeatedEnvironment_ShouldStillProduceOneStateEach()
    {
        var flag = FeatureFlag.Create(
            "new-checkout",
            "New checkout",
            null,
            [EnvironmentKey.Development, EnvironmentKey.Development],
            Now).Value;

        Assert.Equal(EnvironmentKey.All.Count, flag.States.Count);
        Assert.True(flag.IsEnabledIn(EnvironmentKey.Development));
    }

    [Fact]
    public void Create_ShouldAssignUniqueIds()
    {
        var first = FeatureFlag.Create("first", "First", null, Nowhere, Now).Value;
        var second = FeatureFlag.Create("second", "Second", null, Nowhere, Now).Value;

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_WithNullDescription_ShouldDefaultToEmpty()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, Nowhere, Now).Value;

        Assert.Equal(string.Empty, flag.Description);
    }

    [Fact]
    public void Create_ShouldTrimNameAndDescription()
    {
        var flag = FeatureFlag.Create("new-checkout", "  New checkout  ", "  Notes  ", Nowhere, Now).Value;

        Assert.Equal("New checkout", flag.Name);
        Assert.Equal("Notes", flag.Description);
    }

    [Fact]
    public void Create_WithInvalidKey_ShouldPropagateKeyError()
    {
        var result = FeatureFlag.Create("Not A Key", "New checkout", null, Nowhere, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.KeyInvalidFormat, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_ShouldFail(string? name)
    {
        var result = FeatureFlag.Create("new-checkout", name, null, Nowhere, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Create_WithOverlongName_ShouldFail()
    {
        var result = FeatureFlag.Create(
            "new-checkout",
            new string('a', FeatureFlag.MaxNameLength + 1),
            null,
            Nowhere,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.NameTooLong, result.Error);
    }

    [Fact]
    public void Create_WithOverlongDescription_ShouldFail()
    {
        var result = FeatureFlag.Create(
            "new-checkout",
            "New checkout",
            new string('a', FeatureFlag.MaxDescriptionLength + 1),
            Nowhere,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.DescriptionTooLong, result.Error);
    }

    [Fact]
    public void Create_ShouldValidateKeyBeforeName()
    {
        // Both are invalid; the key error wins so callers see a stable first failure.
        var result = FeatureFlag.Create("Not A Key", "", null, Nowhere, Now);

        Assert.Equal(FlagErrors.KeyInvalidFormat, result.Error);
    }

    [Fact]
    public void StateIn_ShouldReturnTheStateForThatEnvironment()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, [EnvironmentKey.Staging], Now).Value;

        var state = flag.StateIn(EnvironmentKey.Staging);

        Assert.True(state.IsSome);
        Assert.True(state.Match(found => found.IsEnabled, () => false));
    }

    [Fact]
    public void SetEnabled_WhenOff_ShouldTurnOnAndStampThatStateOnly()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, Nowhere, Now).Value;
        var later = Now.AddHours(1);

        var result = flag.SetEnabled(EnvironmentKey.Production, isEnabled: true, later);

        Assert.True(result.IsSuccess);
        Assert.True(flag.IsEnabledIn(EnvironmentKey.Production));
        Assert.Equal(later, flag.StateIn(EnvironmentKey.Production).Match(state => state.UpdatedAt, () => default));
    }

    [Fact]
    public void SetEnabled_ShouldLeaveEveryOtherEnvironmentAlone()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, Nowhere, Now).Value;

        flag.SetEnabled(EnvironmentKey.Production, isEnabled: true, Now.AddHours(1));

        Assert.False(flag.IsEnabledIn(EnvironmentKey.Development));
        Assert.False(flag.IsEnabledIn(EnvironmentKey.Staging));
        Assert.Equal(Now, flag.StateIn(EnvironmentKey.Development).Match(state => state.UpdatedAt, () => default));
    }

    [Fact]
    public void SetEnabled_ShouldNotTouchTheFlagsOwnUpdatedAt()
    {
        // The flag's timestamp answers "when did this flag change", which a toggle in one
        // environment does not. Otherwise every view would report a change it did not have.
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, Nowhere, Now).Value;

        flag.SetEnabled(EnvironmentKey.Production, isEnabled: true, Now.AddHours(1));

        Assert.Equal(Now, flag.UpdatedAt);
    }

    [Fact]
    public void SetEnabled_WhenAlreadyInThatState_ShouldNotTouchUpdatedAt()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, [EnvironmentKey.Development], Now).Value;

        var result = flag.SetEnabled(EnvironmentKey.Development, isEnabled: true, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.True(flag.IsEnabledIn(EnvironmentKey.Development));
        Assert.Equal(Now, flag.StateIn(EnvironmentKey.Development).Match(state => state.UpdatedAt, () => default));
    }

    [Fact]
    public void SetEnabled_TurningOff_ShouldDisableAndStamp()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, [EnvironmentKey.Development], Now).Value;
        var later = Now.AddHours(1);

        flag.SetEnabled(EnvironmentKey.Development, isEnabled: false, later);

        Assert.False(flag.IsEnabledIn(EnvironmentKey.Development));
        Assert.Equal(later, flag.StateIn(EnvironmentKey.Development).Match(state => state.UpdatedAt, () => default));
    }
}
