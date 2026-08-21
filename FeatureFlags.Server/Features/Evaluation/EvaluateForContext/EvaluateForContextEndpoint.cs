using FeatureFlags.Domain.SdkKeys;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Evaluation;
using FeatureFlags.Server.Api;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFlags.Server.Features.Evaluation.EvaluateForContext;

public static class EvaluateForContextEndpoint
{
    /// <summary>
    /// Caps on the one route where an authenticated caller decides how much work the server does.
    /// Generous enough that no honest context comes near them, small enough that a dishonest one
    /// cannot turn a bundled publishable key into a way to spend somebody's CPU.
    /// </summary>
    private const int MaxAttributes = 64;
    private const int MaxAttributeNameLength = 100;
    private const int MaxContextKeyLength = 256;
    private const int MaxRequestBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapEvaluateForContext(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/evaluation", async (
            HttpContext context,
            EvaluateForContextRequest request,
            EvaluateForContextHandler handler,
            CancellationToken cancellationToken) =>
        {
            // Before anything is read: a secret key presented from a browser is refused outright,
            // whatever it was asking for. Both key kinds are otherwise welcome here — a server-side
            // caller wanting a one-shot contextual answer without holding the ruleset is a
            // perfectly reasonable thing to be.
            var credential = BrowserCredentialRule.Check(context);
            if (credential.IsFailure)
            {
                return credential.Error.ToProblem();
            }

            var environment = context.User.GetSdkKeyEnvironment()
                .ToResult(SdkKeyErrors.TokenMalformed);

            if (environment.IsFailure)
            {
                return environment.Error.ToProblem();
            }

            var evaluationContext = Bind(request.Context);
            if (evaluationContext.IsFailure)
            {
                return evaluationContext.Error.ToProblem();
            }

            var result = await handler.HandleAsync(
                new EvaluateForContextQuery(environment.Value, evaluationContext.Value),
                cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                error => error.ToProblem());
        })
        .RequireAuthorization(AuthPolicies.SdkKey)
        .RequireCors(BrowserOrigins.PolicyName)
        // Kestrel refuses a larger body before the handler is reached, so an oversized context
        // never becomes work this process does.
        .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
        .WithName("EvaluateFlagsForContext")
        .WithSummary("Every flag's state for one person, in the environment the presented SDK key is scoped to.")
        .Produces<EvaluateForContextResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static Result<FlagContext> Bind(EvaluateForContextContextRequest? request)
    {
        if (request is null)
            return Result.Success(FlagContext.Empty);

        if (request.Key is { Length: > MaxContextKeyLength })
            return Result.Failure<FlagContext>(EvaluationErrors.ContextTooLarge);

        var attributes = request.Attributes ?? new Dictionary<string, AttributeValue>();

        if (attributes.Count > MaxAttributes)
            return Result.Failure<FlagContext>(EvaluationErrors.ContextTooLarge);

        foreach (var attribute in attributes)
        {
            if (attribute.Key.Length > MaxAttributeNameLength)
                return Result.Failure<FlagContext>(EvaluationErrors.ContextTooLarge);

            // A value no engine could agree on — an over-long string, a number past 2^53 — is
            // refused rather than compared. It would never match anything anyway, and saying so is
            // more use than silently answering false to everything.
            if (attribute.Value is null || !attribute.Value.IsRepresentable)
                return Result.Failure<FlagContext>(EvaluationErrors.AttributeNotRepresentable(attribute.Key));
        }

        return Result.Success(new FlagContext(request.Key, attributes));
    }
}
