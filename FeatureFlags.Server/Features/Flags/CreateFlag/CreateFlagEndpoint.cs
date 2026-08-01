using FeatureFlags.Domain.Shared;
using FeatureFlags.Server.Api;

namespace FeatureFlags.Server.Features.Flags.CreateFlag;

public static class CreateFlagEndpoint
{
    public static IEndpointRouteBuilder MapCreateFlag(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/flags", async (
            CreateFlagCommand command,
            CreateFlagHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.Match(
                response => Results.Created($"/api/flags/{response.Key}", response),
                error => error.ToProblem());
        })
        .WithName("CreateFlag")
        .WithSummary("Creates a feature flag.")
        .Produces<CreateFlagResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
