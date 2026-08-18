using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Shared;

namespace FeatureFlags.Server.Features.Flags.GetFlag;

public sealed class GetFlagHandler(IFlagViewRepository viewRepository)
{
    public async Task<Result<GetFlagResponse>> HandleAsync(GetFlagQuery query, CancellationToken cancellationToken = default)
    {
        var flagResult = (await viewRepository.GetByKeyAsync(query.Key, cancellationToken))
            .ToResult(FlagErrors.NotFound(query.Key));

        return flagResult.Map(GetFlagResponse.From);
    }
}
