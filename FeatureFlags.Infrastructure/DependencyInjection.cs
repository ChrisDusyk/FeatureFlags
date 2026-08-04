using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Users;
using FeatureFlags.Infrastructure.Persistence;
using FeatureFlags.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FeatureFlags.Infrastructure;

public static class DependencyInjection
{
    public static TBuilder AddInfrastructure<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddNpgsqlDbContext<AppDbContext>("featureflagsdb");

        builder.Services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        return builder;
    }

    /// <summary>
    /// Brings the database up to the latest migration. Intended for local development against the
    /// Aspire-managed Postgres container; deployed environments should migrate as a deliberate step.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
