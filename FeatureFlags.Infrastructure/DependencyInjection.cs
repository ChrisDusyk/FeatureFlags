using FeatureFlags.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;

namespace FeatureFlags.Infrastructure;

public static class DependencyInjection
{
    public static TBuilder AddInfrastructure<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddNpgsqlDbContext<AppDbContext>("featureflagsdb");

        return builder;
    }
}
