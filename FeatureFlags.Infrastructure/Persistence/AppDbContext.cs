using FeatureFlags.Domain.Flags;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
