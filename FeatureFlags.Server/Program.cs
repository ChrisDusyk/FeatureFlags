using FeatureFlags.Infrastructure;
using FeatureFlags.Server.Api;
using FeatureFlags.Server.Features.Flags.CreateFlag;
using FeatureFlags.Server.Features.Flags.ListFlags;
using FeatureFlags.Server.Features.Flags.ToggleFlag;
using FeatureFlags.Server.Features.Users.GetCurrentUser;
using FeatureFlags.Server.Hosting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Before anything reads configuration: outside the AppHost the connection strings and the auth
// service's address arrive under documented FEATUREFLAGS_* names instead of Aspire's keys.
builder.AddSelfHostConfiguration();

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddRedisClientBuilder("cache")
    .WithOutputCache();
builder.AddInfrastructure();
builder.AddConsoleAuthentication();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Better Auth runs in its own Node process. Forwarding to it from here rather than exposing it
// directly is what keeps the console on one origin, which is what keeps its session cookie
// first-party — see the /api/auth route below.
builder.Services.AddHttpForwarder();

// Feature slice handlers.
builder.Services.AddScoped<CreateFlagHandler>();
builder.Services.AddScoped<ListFlagsHandler>();
builder.Services.AddScoped<ToggleFlagHandler>();
builder.Services.AddScoped<GetCurrentUserHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Deployed, the console is reached through a TLS-terminating proxy — Caddy in the compose bundle,
// an ingress controller in the chart. Without this the server believes every request is plain HTTP
// and hands out http:// Location headers on the https:// origin it is actually serving.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // The proxy is a neighbouring container or pod, not loopback, so the default known-proxy list
    // would reject it. Trusting the hop is sound only because nothing else can reach this port:
    // both deployment artifacts keep the server off the public network behind that proxy.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Configuration.GetConsoleOrigin() is not null)
{
    app.UseForwardedHeaders();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.ShouldApplyMigrations(app.Environment))
{
    await app.Services.ApplyMigrationsAsync();
}

// Migrating is the whole job here, so stop rather than go on to serve. An unhandled exception
// above has already left through Main by this point, which is what makes a failed migration a
// non-zero exit and so a failed deployment.
if (app.Configuration.IsMigrateOnly())
{
    return;
}

app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");

// Everything Better Auth owns — sign-in, sign-up, sessions, tokens, the JWKS — is answered by
// the auth service. The console never calls it directly: on this origin the session cookie is
// first-party, and in development Vite's proxy already sends /api here.
app.MapForwarder("/api/auth/{**catch-all}", app.Configuration.GetAuthServiceAddress())
    .WithName("ForwardToAuthService");

api.MapListFlags();
api.MapCreateFlag();
api.MapToggleFlag();
api.MapGetCurrentUser();

app.MapDefaultEndpoints();

app.UseFileServer();

// An unmatched /api route is a caller's mistake, not a console route. Claim it
// here — the literal segment outranks the catch-all below at the same order — so
// a bad API call gets a 404 instead of a 200 full of HTML.
api.MapFallback(() => Results.Problem(
    statusCode: StatusCodes.Status404NotFound,
    title: "Not found",
    detail: "No API endpoint matches this route."));

// The console is a single-page app: every remaining client route has to be
// answered with its shell so deep links survive a reload.
app.MapFallbackToFile("index.html");

app.Run();
