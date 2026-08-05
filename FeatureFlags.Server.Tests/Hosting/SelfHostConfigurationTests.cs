using FeatureFlags.Server.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FeatureFlags.Server.Tests.Hosting;

/// <summary>
/// These variables are the contract with whoever deploys this, so the translation is worth
/// pinning down: getting it wrong means a connection string that silently drops a password or
/// an SSL mode, which fails somewhere far away from the cause.
/// </summary>
public class SelfHostConfigurationTests
{
    [Fact]
    public void ToNpgsqlConnectionString_TranslatesAPostgresUrl()
    {
        var result = SelfHostConfiguration.ToNpgsqlConnectionString(
            "postgres://flags:s3cret@db.example.com:6432/featureflagsdb");

        var settings = Parse(result);

        Assert.Equal("db.example.com", settings["Host"]);
        Assert.Equal("6432", settings["Port"]);
        Assert.Equal("flags", settings["Username"]);
        Assert.Equal("s3cret", settings["Password"]);
        Assert.Equal("featureflagsdb", settings["Database"]);
    }

    [Fact]
    public void ToNpgsqlConnectionString_DefaultsThePort()
    {
        var settings = Parse(SelfHostConfiguration.ToNpgsqlConnectionString(
            "postgresql://flags:s3cret@db.example.com/featureflagsdb"));

        Assert.Equal("5432", settings["Port"]);
    }

    [Fact]
    public void ToNpgsqlConnectionString_DecodesEscapedCredentials()
    {
        // A password containing @ or / cannot be written in a URL any other way, so a provider
        // handing one out percent-encodes it. Passing it through encoded would fail to authenticate.
        var settings = Parse(SelfHostConfiguration.ToNpgsqlConnectionString(
            "postgres://flags%40prod:p%40ss%2Fword@db.example.com/featureflagsdb"));

        Assert.Equal("flags@prod", settings["Username"]);
        Assert.Equal("p@ss/word", settings["Password"]);
    }

    [Fact]
    public void ToNpgsqlConnectionString_CarriesQueryParametersAcross()
    {
        var settings = Parse(SelfHostConfiguration.ToNpgsqlConnectionString(
            "postgres://flags:s3cret@db.example.com/featureflagsdb?sslmode=require"));

        Assert.Equal("Require", settings["SSL Mode"]);
    }

    [Fact]
    public void ToNpgsqlConnectionString_RejectsAnUnrecognisedQueryParameter()
    {
        // Silently dropping one could downgrade a connection that was meant to be encrypted.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelfHostConfiguration.ToNpgsqlConnectionString(
                "postgres://flags:s3cret@db.example.com/featureflagsdb?schema=public"));

        Assert.Contains("schema", exception.Message);
        Assert.Contains(SelfHostConfiguration.DatabaseUrlVariable, exception.Message);
    }

    [Theory]
    // Unparseable: the '/' ends the authority and 'ab' is not a port.
    [InlineData("postgres://flags:ab/cd@db.example.com:5432/featureflagsdb")]
    // Worse — this one parses, into host 'flags', port 12, no credentials at all, and a database
    // named 'xyz@db.example.com:5432/featureflagsdb'. Left alone it fails far from the cause.
    [InlineData("postgres://flags:12/xyz@db.example.com:5432/featureflagsdb")]
    public void ToNpgsqlConnectionString_RejectsAPasswordThatBreaksTheUrl(string url)
    {
        // Generating a password with `openssl rand -base64` produces exactly this, because base64
        // contains '/'. The advice to use hex lives in the message.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelfHostConfiguration.ToNpgsqlConnectionString(url));

        Assert.Contains(SelfHostConfiguration.DatabaseUrlVariable, exception.Message);
        Assert.Contains("percent-encoded", exception.Message);
    }

    [Fact]
    public void ToNpgsqlConnectionString_AcceptsAPercentEncodedPassword()
    {
        var settings = Parse(SelfHostConfiguration.ToNpgsqlConnectionString(
            "postgres://flags:ab%2Fcd@db.example.com:5432/featureflagsdb"));

        Assert.Equal("ab/cd", settings["Password"]);
        Assert.Equal("db.example.com", settings["Host"]);
    }

    [Fact]
    public void ToNpgsqlConnectionString_PassesANativeConnectionStringThrough()
    {
        const string native = "Host=db.example.com;Username=flags;Password=s3cret;Database=featureflagsdb";

        Assert.Equal(native, SelfHostConfiguration.ToNpgsqlConnectionString(native));
    }

    [Theory]
    [InlineData("redis://cache.example.com:6380", "cache.example.com:6380")]
    [InlineData("redis://cache.example.com", "cache.example.com:6379")]
    [InlineData("redis://:s3cret@cache.example.com", "cache.example.com:6379,password=s3cret")]
    [InlineData("rediss://cache.example.com", "cache.example.com:6379,ssl=True")]
    [InlineData("redis://cache.example.com/3", "cache.example.com:6379,defaultDatabase=3")]
    public void ToRedisConfiguration_TranslatesARedisUrl(string url, string expected)
    {
        Assert.Equal(expected, SelfHostConfiguration.ToRedisConfiguration(url));
    }

    [Fact]
    public void ToRedisConfiguration_RejectsAPasswordContainingItsSeparator()
    {
        // Emitting this would split the password at the comma and authenticate with the first
        // fragment, which fails for a reason neither side reports.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            SelfHostConfiguration.ToRedisConfiguration("redis://:se,cret@cache.example.com"));

        Assert.Contains(SelfHostConfiguration.RedisUrlVariable, exception.Message);
        Assert.Contains("comma", exception.Message);
    }

    [Fact]
    public void ToRedisConfiguration_PassesANativeConfigurationStringThrough()
    {
        const string native = "cache.example.com:6379,password=s3cret,abortConnect=false";

        Assert.Equal(native, SelfHostConfiguration.ToRedisConfiguration(native));
    }

    [Fact]
    public void AddSelfHostConfiguration_FillsTheKeysTheApplicationReads()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            [SelfHostConfiguration.DatabaseUrlVariable] = "postgres://flags:s3cret@db.example.com/featureflagsdb",
            [SelfHostConfiguration.RedisUrlVariable] = "redis://cache.example.com",
            [SelfHostConfiguration.AuthUrlVariable] = "http://auth:8080/"
        });

        Assert.Contains("db.example.com", configuration.GetConnectionString("featureflagsdb"));
        Assert.Equal("cache.example.com:6379", configuration.GetConnectionString("cache"));

        // Trailing slash removed: the JWKS address is built by concatenation.
        Assert.Equal("http://auth:8080", configuration["services:auth:http:0"]);
    }

    [Fact]
    public void AddSelfHostConfiguration_LeavesAspiresValuesAlone()
    {
        // The whole design depends on this: the AppHost has already injected these by the time
        // the translation runs, and a self-hosted variable must not shadow them.
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:featureflagsdb"] = "Host=aspire-postgres;Database=featureflagsdb",
            ["services:auth:http:0"] = "http://localhost:41234",
            [SelfHostConfiguration.DatabaseUrlVariable] = "postgres://flags:s3cret@db.example.com/featureflagsdb",
            [SelfHostConfiguration.AuthUrlVariable] = "http://auth:8080"
        });

        Assert.Equal("Host=aspire-postgres;Database=featureflagsdb", configuration.GetConnectionString("featureflagsdb"));
        Assert.Equal("http://localhost:41234", configuration["services:auth:http:0"]);
    }

    [Fact]
    public void AddSelfHostConfiguration_AddsNothingWhenNoVariablesAreSet()
    {
        var configuration = Build([]);

        Assert.Null(configuration.GetConnectionString("featureflagsdb"));
        Assert.Null(configuration["services:auth:http:0"]);
    }

    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("Production", null, false)]
    [InlineData("Production", "true", true)]
    [InlineData("Development", "false", false)]
    // Case-insensitive, which the auth service's own parsing has to match: one variable
    // configures both, and a "True" that only half of them honoured would migrate one schema
    // and not the other — the half that skipped being the one the other depends on.
    [InlineData("Production", "True", true)]
    [InlineData("Production", "TRUE", true)]
    [InlineData("Development", "False", false)]
    public void ShouldApplyMigrations_DefaultsToDevelopmentAndIsOverridable(
        string environmentName,
        string? variable,
        bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SelfHostConfiguration.ApplyMigrationsVariable] = variable
            })
            .Build();

        var environment = new StubEnvironment { EnvironmentName = environmentName };

        Assert.Equal(expected, configuration.ShouldApplyMigrations(environment));
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(SelfHostConfigurationTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void ShouldApplyMigrations_IsImpliedByMigrateOnly()
    {
        // The chart's migration job sets only FEATUREFLAGS_MIGRATE_ONLY. Asking it to migrate and
        // then exit without that implying "migrate" would produce a job that does nothing at all
        // and reports success — the worst available outcome.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SelfHostConfiguration.MigrateOnlyVariable] = "true"
            })
            .Build();

        Assert.True(configuration.ShouldApplyMigrations(new StubEnvironment { EnvironmentName = Environments.Production }));
        Assert.True(configuration.IsMigrateOnly());
    }

    [Fact]
    public void IsMigrateOnly_IsOffWhenUnset()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.False(configuration.IsMigrateOnly());
    }

    private static IConfiguration Build(Dictionary<string, string?> values)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production
        });

        builder.Configuration.AddInMemoryCollection(values);
        builder.AddSelfHostConfiguration();

        return builder.Configuration;
    }

    private static Dictionary<string, string> Parse(string connectionString) =>
        connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim());
}
