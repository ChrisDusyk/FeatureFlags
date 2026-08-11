using Microsoft.Extensions.DependencyInjection;

namespace FeatureFlags.Client.Redis.Tests;

/// <summary>
/// The wiring that does not need a live Redis: <c>AddFeatureFlagsRedisCache</c>'s own argument
/// checking. <see cref="RedisCachedFeatureFlagClientTests"/> covers the parts that do need one.
/// </summary>
public class RegistrationTests
{
    [Fact]
    public void WithoutAddFeatureFlagsFirst_ShouldThrow()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddFeatureFlagsRedisCache());

        Assert.Contains("AddFeatureFlags", exception.Message);
    }

    [Fact]
    public void WithoutAConfigureDelegate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddFeatureFlagsRedisCache((Action<FeatureFlagsRedisCacheOptions>)null!));
    }

    [Fact]
    public void OnANullServiceCollection_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddFeatureFlagsRedisCache());
    }
}
