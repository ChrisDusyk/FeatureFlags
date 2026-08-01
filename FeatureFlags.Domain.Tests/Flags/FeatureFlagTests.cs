using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Tests.Flags;

public class FeatureFlagTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidInput_ShouldSucceed()
    {
        var result = FeatureFlag.Create("new-checkout", "New checkout", "Rolls out the rewritten checkout.", isEnabled: true, Now);

        Assert.True(result.IsSuccess);

        var flag = result.Value;
        Assert.Equal("new-checkout", flag.Key.Value);
        Assert.Equal("New checkout", flag.Name);
        Assert.Equal("Rolls out the rewritten checkout.", flag.Description);
        Assert.True(flag.IsEnabled);
        Assert.Equal(Now, flag.CreatedAt);
        Assert.Equal(Now, flag.UpdatedAt);
        Assert.NotEqual(Guid.Empty, flag.Id);
    }

    [Fact]
    public void Create_ShouldAssignUniqueIds()
    {
        var first = FeatureFlag.Create("first", "First", null, isEnabled: false, Now).Value;
        var second = FeatureFlag.Create("second", "Second", null, isEnabled: false, Now).Value;

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Create_WithNullDescription_ShouldDefaultToEmpty()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, isEnabled: false, Now).Value;

        Assert.Equal(string.Empty, flag.Description);
    }

    [Fact]
    public void Create_ShouldTrimNameAndDescription()
    {
        var flag = FeatureFlag.Create("new-checkout", "  New checkout  ", "  Notes  ", isEnabled: false, Now).Value;

        Assert.Equal("New checkout", flag.Name);
        Assert.Equal("Notes", flag.Description);
    }

    [Fact]
    public void Create_WithInvalidKey_ShouldPropagateKeyError()
    {
        var result = FeatureFlag.Create("Not A Key", "New checkout", null, isEnabled: false, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.KeyInvalidFormat, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_ShouldFail(string? name)
    {
        var result = FeatureFlag.Create("new-checkout", name, null, isEnabled: false, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.NameRequired, result.Error);
    }

    [Fact]
    public void Create_WithOverlongName_ShouldFail()
    {
        var result = FeatureFlag.Create("new-checkout", new string('a', FeatureFlag.MaxNameLength + 1), null, isEnabled: false, Now);

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
            isEnabled: false,
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal(FlagErrors.DescriptionTooLong, result.Error);
    }

    [Fact]
    public void Create_ShouldValidateKeyBeforeName()
    {
        // Both are invalid; the key error wins so callers see a stable first failure.
        var result = FeatureFlag.Create("Not A Key", "", null, isEnabled: false, Now);

        Assert.Equal(FlagErrors.KeyInvalidFormat, result.Error);
    }

    [Fact]
    public void Enable_WhenDisabled_ShouldEnableAndStampUpdatedAt()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, isEnabled: false, Now).Value;
        var later = Now.AddHours(1);

        flag.Enable(later);

        Assert.True(flag.IsEnabled);
        Assert.Equal(later, flag.UpdatedAt);
        Assert.Equal(Now, flag.CreatedAt);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ShouldNotTouchUpdatedAt()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, isEnabled: true, Now).Value;

        flag.Enable(Now.AddHours(1));

        Assert.True(flag.IsEnabled);
        Assert.Equal(Now, flag.UpdatedAt);
    }

    [Fact]
    public void Disable_WhenEnabled_ShouldDisableAndStampUpdatedAt()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, isEnabled: true, Now).Value;
        var later = Now.AddHours(1);

        flag.Disable(later);

        Assert.False(flag.IsEnabled);
        Assert.Equal(later, flag.UpdatedAt);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ShouldNotTouchUpdatedAt()
    {
        var flag = FeatureFlag.Create("new-checkout", "New checkout", null, isEnabled: false, Now).Value;

        flag.Disable(Now.AddHours(1));

        Assert.False(flag.IsEnabled);
        Assert.Equal(Now, flag.UpdatedAt);
    }
}
