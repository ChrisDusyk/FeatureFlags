using System.Security.Claims;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Domain.Users;
using FeatureFlags.Server.Api;

namespace FeatureFlags.Server.Features.Users.GetCurrentUser;

public static class GetCurrentUserEndpoint
{
    public static IEndpointRouteBuilder MapGetCurrentUser(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/me", async (
            ClaimsPrincipal principal,
            GetCurrentUserHandler handler,
            CancellationToken cancellationToken) =>
        {
            var userId = principal.GetUserId().ToResult(UserErrors.NotProvisioned);

            if (userId.IsFailure)
            {
                return userId.Error.ToProblem();
            }

            var result = await handler.HandleAsync(userId.Value, cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                error => error.ToProblem());
        })
        .RequireAuthorization(AuthPolicies.SignedIn)
        .WithName("GetCurrentUser")
        .WithSummary("Returns the signed-in user.")
        .Produces<GetCurrentUserResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
