using FeatureFlags.Domain.SdkKeys;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Features.SdkKeys.ListSdkKeys;

public sealed class ListSdkKeysHandler(ISdkKeyRepository repository)
{
    public async Task<Result<ListSdkKeysResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var keys = await repository.ListAsync(cancellationToken);

        var summaries = keys
            .Select(SdkKeySummary.From)
            .ToList();

        return Result.Success(new ListSdkKeysResponse(summaries));
    }
}
