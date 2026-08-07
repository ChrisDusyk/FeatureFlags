using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlags.Client.Internal;

/// <summary>
/// The one request this package makes: <c>GET /api/evaluation</c>, conditionally.
/// </summary>
internal sealed class EvaluationApiClient
{
    /// <summary>
    /// Relative, so it composes with whatever path the installation is served under. No leading
    /// slash for the same reason — one would discard any base path.
    /// </summary>
    private const string Path = "api/evaluation";

    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public EvaluationApiClient(HttpClient http) => _http = http;

    /// <summary>
    /// Fetches, sending the previous ETag if there is one. Returns null when the server answers 304
    /// — the caller already holds that answer and should keep it.
    /// </summary>
    /// <exception cref="FeatureFlagsException">The server refused or answered with nonsense.</exception>
    public async Task<FlagSnapshot?> FetchAsync(
        FlagSnapshot? current,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        if (current?.ETag is { Length: > 0 } etag)
        {
            // TryAddWithoutValidation: the tag is the server's own, echoed back verbatim. Parsing
            // and re-serialising it is a chance to change it, and a changed validator never matches.
            request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified && current is not null)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new FeatureFlagsException(
                "The FeatureFlags server rejected this SDK key. It may have been revoked, or it may " +
                "belong to a different installation.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new FeatureFlagsException(
                $"The FeatureFlags server answered {(int)response.StatusCode} for {Path}.");
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        EvaluationPayload? payload;

        try
        {
            payload = JsonSerializer.Deserialize<EvaluationPayload>(body, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new FeatureFlagsException(
                "The FeatureFlags server's response could not be read. This usually means something " +
                "other than the API answered — a proxy, or a login page.",
                exception);
        }

        if (payload?.Flags is null || payload.Environment is null)
        {
            throw new FeatureFlagsException("The FeatureFlags server's response was missing its flags.");
        }

        return new FlagSnapshot(
            payload.Environment,
            new Dictionary<string, bool>(payload.Flags, StringComparer.OrdinalIgnoreCase),
            response.Headers.ETag?.ToString(),
            now);
    }

    /// <summary>
    /// The wire shape. Kept private to this package: it is the server's response, not something a
    /// caller should be handed.
    /// </summary>
    private sealed class EvaluationPayload
    {
        public string? Environment { get; set; }

        public Dictionary<string, bool>? Flags { get; set; }
    }
}
