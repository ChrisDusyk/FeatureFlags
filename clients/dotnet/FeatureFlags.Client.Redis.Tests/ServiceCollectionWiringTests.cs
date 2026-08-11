using FeatureFlags.Client.Internal;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace FeatureFlags.Client.Redis.Tests;

/// <summary>
/// <c>AddFeatureFlagsRedisCache</c> end to end: a container built the way a real application would
/// build one, reading a flag through the whole chain from DI registration down to Redis.
/// </summary>
[Collection(nameof(RedisCollection))]
public sealed class ServiceCollectionWiringTests(RedisFixture redis)
{
    private const string SdkKey =
        "ffs_dev_f992c8928754087a_7f097037aa14d671f4317df877989f05f5309c1323ecb24dab4be5597f40db10";

    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddFeatureFlagsRedisCache_ShouldResolveARedisBackedClient_ThatAnswersAFlag()
    {
        var server = new StubHandler().AnswersWithFlags("dev", new { on = true }, "\"v1\"");
        var keyPrefix = RedisFixture.NewKeyPrefix();

        // A connection of this test's own, not the fixture sharing one: the container will dispose
        // whatever IConnectionMultiplexer ends up registered when it tears down (RedisCache.Dispose
        // closes and disposes it regardless of who created it), so it must not be one another test
        // still needs.
        using var multiplexer = ConnectionMultiplexer.Connect(redis.ConnectionString);

        var services = new ServiceCollection();
        services.AddLogging();

        // The default path an Aspire-orchestrated (or otherwise DI-registered) consumer takes: the
        // multiplexer is already in the container, and AddFeatureFlagsRedisCache() with no argument
        // picks it up rather than needing to be told where Redis is a second time.
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        services.AddFeatureFlags(options =>
        {
            options.BaseAddress = new Uri("https://flags.example.com");
            options.SdkKey = SdkKey;
        });

        services.AddFeatureFlagsRedisCache(o => o.KeyPrefix = keyPrefix);

        // The stub only answers HTTP calls; AddFeatureFlags' own HttpClient registration would
        // otherwise try to reach the real https://flags.example.com.
        services.AddHttpClient<EvaluationApiClient>().ConfigurePrimaryHttpMessageHandler(() => server);

        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IFeatureFlagClient>();

        Assert.IsType<RedisCachedFeatureFlagClient>(client);
        Assert.True(await client.IsEnabledAsync("on", Cancellation));
    }
}
