using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Domain.Users;

/// <summary>
/// Read-only by design: the mirror is written by a database trigger, never by the application.
/// </summary>
public interface IUserRepository
{
    Task<Option<User>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
