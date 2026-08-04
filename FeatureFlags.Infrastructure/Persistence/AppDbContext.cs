using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    /// <summary>
    /// A read-only mirror of <c>auth.user</c>, maintained by a trigger rather than by EF.
    /// Tracking changes here would be misleading — nothing in this application writes it.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
