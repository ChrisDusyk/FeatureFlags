using System;
using System.Linq;
using System.Threading.Tasks;
using FeatureFlags.Client.Internal;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace FeatureFlags.Client.Redis;

/// <summary>
/// Layers a Redis cache tier onto a client already registered by <c>AddFeatureFlags</c>.
///
/// <code>
/// services.AddFeatureFlags(options => { ... });
/// services.AddFeatureFlagsRedisCache(); // resolves IConnectionMultiplexer from this container
/// </code>
/// </summary>
public static class FeatureFlagsRedisCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Redis tier with default options, reading <see cref="IConnectionMultiplexer"/> from
    /// this application's own <c>IServiceCollection</c>.
    /// </summary>
    public static IServiceCollection AddFeatureFlagsRedisCache(this IServiceCollection services) =>
        AddFeatureFlagsRedisCache(services, static _ => { });

    /// <summary>Adds the Redis tier, configured in code.</summary>
    public static IServiceCollection AddFeatureFlagsRedisCache(
        this IServiceCollection services,
        Action<FeatureFlagsRedisCacheOptions> configure)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        // AddFeatureFlags registers EvaluationApiClient as a typed HttpClient. Requiring it first,
        // loudly, beats a NullReferenceException the first time something reads a flag — the whole
        // point of this check is to fail at startup instead of at a request.
        if (services.All(descriptor => descriptor.ServiceType != typeof(EvaluationApiClient)))
        {
            throw new InvalidOperationException(
                "AddFeatureFlagsRedisCache requires AddFeatureFlags to be called first — it reuses " +
                "the HTTP client and options that call registers.");
        }

        var redisOptions = new FeatureFlagsRedisCacheOptions();
        configure(redisOptions);

        services
            .AddFusionCache()
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDistributedCache(provider => new RedisCache(new RedisCacheOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(ResolveMultiplexer(provider, redisOptions))
            }))
            .WithBackplane(provider => new RedisBackplane(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(ResolveMultiplexer(provider, redisOptions))
            }));

        // Not TryAdd: this is meant to replace the in-memory-only client AddFeatureFlags registered.
        // The last registration wins when IFeatureFlagClient is resolved singly, which is every
        // caller of it — FeatureFlagsRefreshService included, so the same background polling loop
        // keeps this tier warm too, it just refreshes through Redis instead of only in memory.
        services.AddSingleton<IFeatureFlagClient>(provider => new RedisCachedFeatureFlagClient(
            provider.GetRequiredService<EvaluationApiClient>(),
            provider.GetRequiredService<IFusionCache>(),
            provider.GetRequiredService<IOptions<FeatureFlagsOptions>>(),
            redisOptions,
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    private static IConnectionMultiplexer ResolveMultiplexer(
        IServiceProvider provider,
        FeatureFlagsRedisCacheOptions options) =>
        options.ConnectionMultiplexerFactory is { } factory
            ? factory(provider)
            : provider.GetRequiredService<IConnectionMultiplexer>();
}
