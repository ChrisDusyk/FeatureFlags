using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
