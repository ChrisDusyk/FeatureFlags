using FeatureFlags.Domain.Shared;
using FeatureFlags.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<Option<User>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return user.ToOption();
    }
}
