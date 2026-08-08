using System;
using System.Net.Http.Headers;
using System.Reflection;
using FeatureFlags.Client.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FeatureFlags.Client;

/// <summary>
/// Registers <see cref="IFeatureFlagClient"/>.
///
/// <code>
/// services.AddFeatureFlags(options =>
/// {
///     options.BaseAddress = new Uri("https://flags.example.com");
///     options.SdkKey = configuration["FeatureFlags:SdkKey"];
/// });
/// </code>
/// </summary>
public static class FeatureFlagsServiceCollectionExtensions
{
    private static readonly string Version =
        typeof(FeatureFlagsServiceCollectionExtensions).GetTypeInfo().Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    /// <summary>Registers the client, configured in code.</summary>
    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        Action<FeatureFlagsOptions> configure)
    {
        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.Configure(configure);

        return AddFeatureFlagsCore(services);
    }

    /// <summary>
    /// Binds from configuration — an <c>appsettings.json</c> section, environment variables, or
    /// anything else bound into it.
    ///
    /// <code>
    /// {
    ///   "FeatureFlags": {
    ///     "BaseAddress": "https://flags.example.com",
    ///     "SdkKey": "ffs_prod_…"
    ///   }
    /// }
    /// </code>
    /// </summary>
    public static IServiceCollection AddFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        services.Configure<FeatureFlagsOptions>(configuration);

        return AddFeatureFlagsCore(services);
    }

    private static IServiceCollection AddFeatureFlagsCore(IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<FeatureFlagsOptions>, FeatureFlagsOptionsValidator>());

        services.AddHttpClient<EvaluationApiClient>((provider, http) =>
        {
            var options = provider.GetRequiredService<IOptions<FeatureFlagsOptions>>().Value;

            // Trailing slash: without one, Uri composition drops the last path segment, so an
            // installation served under a sub-path would silently lose it.
            var baseAddress = options.BaseAddress!.AbsoluteUri;
            http.BaseAddress = new Uri(baseAddress.EndsWith("/", StringComparison.Ordinal)
                ? baseAddress
                : baseAddress + "/");

            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.SdkKey);
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("FeatureFlags.Client", Version));

            // The client's own Timeout bounds a refresh, including the wait for a free slot, so
            // this one is a backstop rather than the mechanism.
            http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        });

        services.TryAddSingleton<IFeatureFlagClient, FeatureFlagClient>();

        // Hosted, so a hosted application warms the snapshot at startup and polls. Nothing depends
        // on it: the client refreshes lazily as well, which is what keeps this package usable from
        // a console application or a .NET Framework service with no host at all.
        services.AddHostedService<FeatureFlagsRefreshService>();

        return services;
    }
}
