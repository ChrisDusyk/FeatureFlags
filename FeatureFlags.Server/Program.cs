using FeatureFlags.Infrastructure;
using FeatureFlags.Server.Api;
using FeatureFlags.Server.Features.Flags.CreateFlag;
using FeatureFlags.Server.Features.Users.GetCurrentUser;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<GetCurrentUserHandler>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await app.Services.ApplyMigrationsAsync();
}

app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

var api = app.MapGroup("/api");

// Everything Better Auth owns — sign-in, sign-up, sessions, tokens, the JWKS — is answered by
// the auth service. The console never calls it directly: on this origin the session cookie is
// first-party, and in development Vite's proxy already sends /api here.
app.MapForwarder("/api/auth/{**catch-all}", app.Configuration.GetAuthServiceAddress())
    .WithName("ForwardToAuthService");

api.MapCreateFlag();
api.MapGetCurrentUser();

api.MapGet("weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)))
.WithName("GetWeatherForecast");

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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
