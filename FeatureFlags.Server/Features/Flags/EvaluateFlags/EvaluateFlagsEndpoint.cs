using FeatureFlags.Domain.SdkKeys;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Server.Api;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FeatureFlags.Server.Features.Flags.EvaluateFlags;

public static class EvaluateFlagsEndpoint
{
    public static IEndpointRouteBuilder MapEvaluateFlags(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/evaluation", async (
            HttpContext context,
            EvaluateFlagsHandler handler,
            CancellationToken cancellationToken) =>
        {
            // The policy already guarantees an SDK key authenticated this request, so a missing
            // environment claim is this server contradicting itself rather than a caller's mistake.
            // It still has to be answered for rather than assumed, which is what Option is for.
            var environment = context.User.GetSdkKeyEnvironment()
                .ToResult(SdkKeyErrors.TokenMalformed);

            if (environment.IsFailure)
            {
                return environment.Error.ToProblem();
            }

            var result = await handler.HandleAsync(
                new EvaluateFlagsQuery(environment.Value),
                cancellationToken);

            return result.Match(
                evaluated => Respond(context, evaluated),
                error => error.ToProblem());
        })
        .RequireAuthorization(AuthPolicies.SdkKey)
        .WithName("EvaluateFlags")
        .WithSummary("Every flag's state in the environment the presented SDK key is scoped to.")
        .Produces<EvaluateFlagsResponse>()
        .Produces(StatusCodes.Status304NotModified)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    /// <summary>
    /// Answers with the body, or with a 304 when the caller already has it.
    ///
    /// <para>
    /// <c>no-cache</c> is not "do not cache" — it is "hold it, but ask before using it", which is
    /// exactly the arrangement the ETag sets up. <c>private</c> because the answer depends on the
    /// key that asked, and a shared proxy serving one environment's flags to another environment's
    /// client would be the worst kind of bug to find.
    /// </para>
    /// </summary>
    private static IResult Respond(HttpContext context, EvaluatedFlags evaluated)
    {
        context.Response.Headers.CacheControl = "no-cache, private";
        context.Response.Headers.ETag = evaluated.ETag;

        return IsUnchanged(context.Request.Headers[HeaderNames.IfNoneMatch], evaluated.ETag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Ok(evaluated.Response);
    }

    /// <summary>
    /// Whether the caller already holds this version. <c>If-None-Match</c> is a comma-separated
    /// list and may repeat as a header, so both are flattened; <c>*</c> means "any version I have".
    /// A weak-comparison prefix is accepted because this is a cache validator, not a range request —
    /// nothing here distinguishes a weak tag from a strong one.
    /// </summary>
    private static bool IsUnchanged(StringValues presented, string etag) =>
        presented
            .SelectMany(value => value?.Split(',') ?? [])
            .Select(candidate => candidate.Trim())
            .Any(candidate =>
                candidate == "*" ||
                candidate == etag ||
                (candidate.StartsWith("W/", StringComparison.Ordinal) && candidate[2..] == etag));
}
