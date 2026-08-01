using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Features.Flags.CreateFlag;

public sealed class CreateFlagHandler(IFeatureFlagRepository repository, TimeProvider timeProvider)
{
    public async Task<Result<CreateFlagResponse>> HandleAsync(
        CreateFlagCommand command,
        CancellationToken cancellationToken = default)
    {
        var flagResult = FeatureFlag.Create(
            command.Key,
            command.Name,
            command.Description,
            command.IsEnabled,
            timeProvider.GetUtcNow());

        if (flagResult.IsFailure)
            return Result.Failure<CreateFlagResponse>(flagResult.Error);

        var flag = flagResult.Value;

        // Checked here so the caller gets a Conflict rather than an exception from the unique
        // index. The index remains the real guard against a concurrent insert of the same key.
        var existing = await repository.GetByKeyAsync(flag.Key, cancellationToken);
        if (existing.IsSome)
            return Result.Failure<CreateFlagResponse>(FlagErrors.DuplicateKey(flag.Key));

        await repository.AddAsync(flag, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(CreateFlagResponse.From(flag));
    }
}
