using FeatureFlags.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FeatureFlags.Server.Api;

/// <summary>
/// Wires up the server's half of authentication: it trusts tokens the auth service signed, and
/// nothing else. There is no session store and no per-request call out — a token carries the
/// user's id and role, and the signature is checked against the auth service's public keys.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Must match the values the auth service puts in the tokens it mints (auth/src/config.ts).
    /// Deliberately not derived from a hostname, so neither side needs reconfiguring when a URL
    /// changes; trust comes from the signature, not from these strings.
    /// </summary>
    private const string Issuer = "featureflags-auth";
    private const string Audience = "featureflags-api";

    private const string JwksPath = "/api/auth/jwks";

    public static TBuilder AddConsoleAuthentication<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var authAddress = builder.Configuration.GetAuthServiceAddress();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the claim names the token actually uses. Without this ASP.NET rewrites
                // "sub" and "role" into WS-Federation URIs, and AuthClaims stops lining up.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Issuer,
                    ValidAudience = Audience,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = AuthClaims.Email,
                    RoleClaimType = AuthClaims.Role,
                    // Tokens live 15 minutes; there is no reason to honour a stale one for longer.
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // The auth service publishes a bare JWKS rather than an OpenID discovery
                // document, so the metadata is assembled from those keys directly. The
                // configuration manager still handles caching and periodic refresh, which is
                // what makes key rotation a non-event here.
                options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{authAddress.TrimEnd('/')}{JwksPath}",
                    new JwksConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = false });
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.SignedIn, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(AuthPolicies.Admin, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(UserRole.Admin.Value));

        return builder;
    }

    /// <summary>
    /// The auth service's base address, as Aspire's <c>WithReference(auth)</c> injects it.
    /// Absent means the server was started outside the AppHost, which is worth failing over
    /// immediately rather than at somebody's first sign-in.
    /// </summary>
    public static string GetAuthServiceAddress(this IConfiguration configuration) =>
        configuration["services:auth:http:0"]
        ?? throw new InvalidOperationException(
            "The auth service address is not configured. Run the app through the Aspire AppHost.");
}

/// <summary>
/// Turns the auth service's JWKS into the metadata the JWT handler expects. Only the signing
/// keys matter — everything else in an OpenID configuration is unused here.
/// </summary>
internal sealed class JwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancel)
    {
        var document = await retriever.GetDocumentAsync(address, cancel);

        var configuration = new OpenIdConnectConfiguration
        {
            JwksUri = address,
            JsonWebKeySet = new JsonWebKeySet(document)
        };

        foreach (var key in configuration.JsonWebKeySet.GetSigningKeys())
        {
            configuration.SigningKeys.Add(key);
        }

        return configuration;
    }
}
