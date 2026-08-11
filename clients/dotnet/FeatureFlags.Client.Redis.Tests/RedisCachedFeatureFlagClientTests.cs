using FeatureFlags.Client.Internal;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FeatureFlags.Client.Redis.Tests;

/// <summary>
/// Against a real Redis (one Testcontainers instance for the whole collection — see
/// <see cref="RedisFixture"/>): the behaviors that only exist once there is an actual L2 and a real
/// backplane behind the in-memory tier, which nothing about the base package's own tests can cover.
/// </summary>
[Collection(nameof(RedisCollection))]
public sealed class RedisCachedFeatureFlagClientTests(RedisFixture redis)
{
    private const string SdkKey =
        "ffs_dev_f992c8928754087a_7f097037aa14d671f4317df877989f05f5309c1323ecb24dab4be5597f40db10";

    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    // A dedicated connection per cache instance, not the fixture sharing one: RedisCache.Dispose()
    // closes and disposes whatever IConnectionMultiplexer it is given regardless of who created it,
    // so a shared multiplexer gets pulled out from under any test still running once an earlier
    // test's FusionCache/RedisCache is garbage collected and finalized.
    private IFusionCache BuildCache()
    {
        var multiplexer = ConnectionMultiplexer.Connect(redis.ConnectionString);

        return new FusionCache(Options.Create(new FusionCacheOptions()))
            .SetupSerializer(new FusionCacheSystemTextJsonSerializer())
            .SetupDistributedCache(new RedisCache(new RedisCacheOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer)
            }))
            .SetupBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(multiplexer)
            }));
    }

    private RedisCachedFeatureFlagClient CreateSut(
        StubHandler server,
        string keyPrefix,
        TimeSpan? pollingInterval = null,
        TimeSpan? failSafeMaxDuration = null) =>
        new(
            new EvaluationApiClient(new HttpClient(server) { BaseAddress = new Uri("https://flags.example.com/") }),
            BuildCache(),
            Options.Create(new FeatureFlagsOptions
            {
                BaseAddress = new Uri("https://flags.example.com"),
                SdkKey = SdkKey,
                PollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(200)
            }),
            new FeatureFlagsRedisCacheOptions
            {
                KeyPrefix = keyPrefix,
                FailSafeMaxDuration = failSafeMaxDuration ?? TimeSpan.FromHours(24),
                FailSafeThrottleDuration = TimeSpan.FromMilliseconds(50)
            },
            TimeProvider.System);

    [Fact]
    public async Task WhenTheOriginFails_AStaleRedisValue_IsStillServed()
    {
        var server = new StubHandler().AnswersWithFlags("dev", new { on = true }, "\"v1\"");
        var sut = CreateSut(server, RedisFixture.NewKeyPrefix());

        Assert.True(await sut.IsEnabledAsync("on", Cancellation));

        // The origin is gone, and the entry's 200ms Duration has elapsed — a healthy read would
        // refetch and get nothing to work with. Fail-safe should answer from Redis instead.
        server.Throws();
        await Task.Delay(400, Cancellation);

        Assert.True(await sut.IsEnabledAsync("on", defaultValue: false, Cancellation));
    }

    [Fact]
    public async Task ASecondColdInstance_ReadsTheFirstInstancesValue_WithoutCallingTheOrigin()
    {
        var keyPrefix = RedisFixture.NewKeyPrefix();

        var firstServer = new StubHandler().AnswersWithFlags("dev", new { on = true }, "\"v1\"");
        var first = CreateSut(firstServer, keyPrefix, pollingInterval: TimeSpan.FromMinutes(1));
        Assert.True(await first.IsEnabledAsync("on", Cancellation));

        // A second instance, its own FusionCache and own empty L1, pointed at the same Redis. If it
        // reads Redis before ever asking the origin, this server — which refuses every request —
        // never gets called.
        var secondServer = new StubHandler().Throws();
        var second = CreateSut(secondServer, keyPrefix, pollingInterval: TimeSpan.FromMinutes(1));

        Assert.True(await second.IsEnabledAsync("on", Cancellation));
        Assert.Equal(0, secondServer.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_ThrowsOnFailure_MatchingTheInterfaceContract()
    {
        var server = new StubHandler().Throws();
        var sut = CreateSut(server, RedisFixture.NewKeyPrefix());

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.RefreshAsync(Cancellation));
    }

    [Fact]
    public async Task RefreshAsync_PublishesToOtherInstancesSharingTheBackplane()
    {
        var keyPrefix = RedisFixture.NewKeyPrefix();

        var firstServer = new StubHandler().AnswersWithFlags("dev", new { on = true }, "\"v1\"");
        var first = CreateSut(firstServer, keyPrefix, pollingInterval: TimeSpan.FromMinutes(1));

        var secondServer = new StubHandler().AnswersWithFlags("dev", new { on = true }, "\"v1\"");
        var second = CreateSut(secondServer, keyPrefix, pollingInterval: TimeSpan.FromMinutes(1));

        Assert.True(await first.IsEnabledAsync("on", Cancellation));
        Assert.True(await second.IsEnabledAsync("on", Cancellation));

        var callsBeforeTheFlip = secondServer.CallCount;

        // Only the first instance is told about the flip. The second should learn of it through the
        // backplane, not by outliving its own minute-long Duration or asking its own origin again.
        firstServer.AnswersWithFlags("dev", new { on = false }, "\"v2\"");
        await first.RefreshAsync(Cancellation);

        var flipped = false;

        for (var i = 0; i < 30 && !flipped; i++)
        {
            await Task.Delay(100, Cancellation);
            flipped = !await second.IsEnabledAsync("on", Cancellation);
        }

        Assert.True(flipped);
        Assert.Equal(callsBeforeTheFlip, secondServer.CallCount);
    }

    [Fact]
    public async Task ANotModifiedResponse_ShouldKeepThePreviousAnswer()
    {
        var server = new StubHandler().AnswersWithFlags("dev", new { on = true }, "\"v1\"");
        var sut = CreateSut(server, RedisFixture.NewKeyPrefix());

        Assert.True(await sut.IsEnabledAsync("on", Cancellation));

        server.AnswersNotModified("\"v1\"");
        await Task.Delay(400, Cancellation);

        Assert.True(await sut.IsEnabledAsync("on", Cancellation));
    }
}
