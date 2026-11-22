using Atomiq.Application.Abstractions;

namespace Atomiq.Application.Reactions;

public sealed class GetReactionHistoryUseCase(IReactionHistoryRepository reactions)
{
    public async Task<IReadOnlyList<ReactionLogDto>> ExecuteAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var items = await reactions.GetByDeviceIdAsync(deviceId, from, to, cancellationToken);
        return items.Select(ReactionMappings.ToDto).ToArray();
    }
}
