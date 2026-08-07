using System.Security.Claims;
using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Domain.Users;
using FeatureFlags.Server.Api;

namespace FeatureFlags.Server.Features.SdkKeys.IssueSdkKey;

public static class IssueSdkKeyEndpoint
{
    public static IEndpointRouteBuilder MapIssueSdkKey(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/sdk-keys", async (
            IssueSdkKeyRequest request,
            ClaimsPrincipal principal,
            IssueSdkKeyHandler handler,
            CancellationToken cancellationToken) =>
        {
            var environment = EnvironmentKey.Create(request.Environment);

            if (environment.IsFailure)
            {
                return environment.Error.ToProblem();
            }

            var issuedBy = principal.GetUserId().ToResult(UserErrors.NotProvisioned);

            if (issuedBy.IsFailure)
            {
                return issuedBy.Error.ToProblem();
            }

            var result = await handler.HandleAsync(
                new IssueSdkKeyCommand(request.Name, environment.Value, issuedBy.Value),
                cancellationToken);

            return result.Match(
                // No Location header: there is no route that answers with a key, by design. The
                // token in this body is the only time it exists, and a URL suggesting otherwise
                // would be an invitation to go and fetch it again.
                response => Results.Created((string?)null, response),
                error => error.ToProblem());
        })
        .RequireAuthorization(AuthPolicies.Admin)
        .WithName("IssueSdkKey")
        .WithSummary("Issues an SDK key for one environment. The token is returned once and cannot be recovered.")
        .Produces<IssueSdkKeyResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}
