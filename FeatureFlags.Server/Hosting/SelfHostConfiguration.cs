using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace FeatureFlags.Server.Hosting;

/// <summary>
/// Translates the documented <c>FEATUREFLAGS_*</c> environment variables a self-hosting operator
/// sets into the configuration keys this application actually reads.
///
/// Those keys are Aspire's shape — <c>ConnectionStrings:featureflagsdb</c>, <c>services:auth:http:0</c>
/// — and they are an implementation detail of how the AppHost wires resources together. Nobody
/// running a container should have to learn them, and building the deployment surface on top of
/// them would make an Aspire convention change a breaking change for every consumer.
///
/// Nothing here overwrites a key that is already set, so the AppHost is unaffected: Aspire's
/// injected values are in configuration by the time this runs, and they win.
/// </summary>
public static class SelfHostConfiguration
{
    /// <summary>The origin a browser sees, e.g. <c>https://flags.example.com</c>.</summary>
    public const string OriginVariable = "FEATUREFLAGS_ORIGIN";

    public const string DatabaseUrlVariable = "FEATUREFLAGS_DATABASE_URL";
    public const string RedisUrlVariable = "FEATUREFLAGS_REDIS_URL";
    public const string AuthUrlVariable = "FEATUREFLAGS_AUTH_URL";
    public const string ApplyMigrationsVariable = "FEATUREFLAGS_APPLY_MIGRATIONS";
    public const string MigrateOnlyVariable = "FEATUREFLAGS_MIGRATE_ONLY";

    private const string DatabaseConnectionStringKey = "ConnectionStrings:featureflagsdb";
    private const string CacheConnectionStringKey = "ConnectionStrings:cache";
    private const string AuthServiceAddressKey = "services:auth:http:0";

    private const int DefaultPostgresPort = 5432;
    private const int DefaultRedisPort = 6379;

    public static TBuilder AddSelfHostConfiguration<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var translated = new Dictionary<string, string?>();

        Translate(builder.Configuration, translated, DatabaseConnectionStringKey, DatabaseUrlVariable, ToNpgsqlConnectionString);
        Translate(builder.Configuration, translated, CacheConnectionStringKey, RedisUrlVariable, ToRedisConfiguration);
        Translate(builder.Configuration, translated, AuthServiceAddressKey, AuthUrlVariable, address => address.TrimEnd('/'));

        if (translated.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(translated);
        }

        return builder;
    }

    /// <summary>
    /// The origin a browser reaches the console on, when one has been configured. Absent under the
    /// AppHost, where Vite and Aspire's own hostnames decide it — which is why the caller treats
    /// absence as "not behind a proxy" rather than as a misconfiguration.
    /// </summary>
    public static string? GetConsoleOrigin(this IConfiguration configuration) =>
        configuration[OriginVariable] is { Length: > 0 } origin ? origin : null;

    /// <summary>
    /// Whether to bring the database up to the latest migration during startup.
    ///
    /// On by default in Development, which is what the AppHost relies on. Off everywhere else
    /// unless asked for: the compose bundle sets it because it is single-replica by construction,
    /// while the Helm chart defaults to running migrations as a job instead.
    /// </summary>
    public static bool ShouldApplyMigrations(this IConfiguration configuration, IHostEnvironment environment) =>
        configuration.IsMigrateOnly()
        || (configuration.GetValue<bool?>(ApplyMigrationsVariable) ?? environment.IsDevelopment());

    /// <summary>
    /// Whether to migrate and then exit rather than go on to serve.
    ///
    /// What makes a migration something a deployment can order. The Helm chart's pre-upgrade job
    /// runs the server this way so that a failed migration fails the release before any deployment
    /// is touched — a server that migrated and then started serving would never finish the job.
    /// </summary>
    public static bool IsMigrateOnly(this IConfiguration configuration) =>
        configuration.GetValue<bool>(MigrateOnlyVariable);

    private static void Translate(
        IConfigurationManager configuration,
        Dictionary<string, string?> translated,
        string key,
        string variable,
        Func<string, string> translation)
    {
        // An already-populated key means Aspire (or the operator, directly) got there first.
        if (!string.IsNullOrWhiteSpace(configuration[key]))
        {
            return;
        }

        if (configuration[variable] is not { Length: > 0 } value)
        {
            return;
        }

        translated[key] = translation(value.Trim());
    }

    /// <summary>
    /// Turns a <c>postgres://</c> URL into the keyword-value string Npgsql expects. Anything that
    /// is not such a URL is passed through untouched, so an operator who would rather write a
    /// native connection string can.
    /// </summary>
    public static string ToNpgsqlConnectionString(string value)
    {
        if (!TryParseUrl(value, out var url, "postgres", "postgresql"))
        {
            return value;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.Port > 0 ? url.Port : DefaultPostgresPort,
            Database = Unescape(url.AbsolutePath.TrimStart('/'))
        };

        var (user, password) = SplitUserInfo(url.UserInfo);

        if (user is not null)
        {
            builder.Username = user;
        }

        if (password is not null)
        {
            builder.Password = password;
        }

        // Hosted Postgres providers put meaningful settings here — `?sslmode=require` most of all.
        // Dropping an unrecognised one silently could quietly downgrade a connection's security,
        // so an unusable parameter is reported rather than ignored.
        foreach (var (parameter, parameterValue) in ParseQuery(url.Query))
        {
            try
            {
                builder[parameter] = parameterValue;
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    $"{DatabaseUrlVariable} carries the query parameter '{parameter}', which Npgsql does not recognise. " +
                    "Remove it, or set the variable to a native Npgsql connection string instead.",
                    exception);
            }
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Turns a <c>redis://</c> URL into StackExchange.Redis' comma-separated configuration string.
    /// As with Postgres, anything else is passed through.
    ///
    /// A password containing a comma cannot survive this translation, because that is the
    /// separator StackExchange.Redis uses and it offers no escape for it. Such a password has to
    /// be given as a native configuration string.
    /// </summary>
    public static string ToRedisConfiguration(string value)
    {
        if (!TryParseUrl(value, out var url, "redis", "rediss"))
        {
            return value;
        }

        var port = url.Port > 0 ? url.Port : DefaultRedisPort;
        var options = new List<string> { $"{url.Host}:{port}" };

        var (user, password) = SplitUserInfo(url.UserInfo);

        if (password is not null)
        {
            options.Add($"password={password}");
        }

        // Redis 6 ACL users. The URL's `default` user is what a passwordless server assumes
        // anyway, so carrying it across would only add noise.
        if (user is { Length: > 0 } and not "default")
        {
            options.Add($"user={user}");
        }

        if (url.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
        {
            options.Add("ssl=True");
        }

        if (url.AbsolutePath.TrimStart('/') is { Length: > 0 } database)
        {
            options.Add($"defaultDatabase={database}");
        }

        return string.Join(',', options);
    }

    private static bool TryParseUrl(string value, [NotNullWhen(true)] out Uri? url, params string[] schemes)
    {
        url = null;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!schemes.Contains(parsed.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        url = parsed;
        return true;
    }

    private static (string? User, string? Password) SplitUserInfo(string userInfo)
    {
        if (userInfo.Length == 0)
        {
            return (null, null);
        }

        var separator = userInfo.IndexOf(':');

        return separator < 0
            ? (Unescape(userInfo), null)
            : (Unescape(userInfo[..separator]), Unescape(userInfo[(separator + 1)..]));
    }

    private static IEnumerable<(string Name, string Value)> ParseQuery(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');

            if (separator > 0)
            {
                yield return (Unescape(pair[..separator]), Unescape(pair[(separator + 1)..]));
            }
        }
    }

    /// <summary>
    /// Credentials reach these URLs percent-encoded, because a password containing <c>@</c> or
    /// <c>/</c> could not be written in one otherwise.
    /// </summary>
    private static string Unescape(string value) => Uri.UnescapeDataString(value);
}
