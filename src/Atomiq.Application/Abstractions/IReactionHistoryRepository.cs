using Atomiq.Domain;

namespace Atomiq.Application.Abstractions;

public interface IReactionHistoryRepository
{
    Task<ReactionLog?> TryCreateAsync(
        ReactionLog reaction,
        CancellationToken cancellationToken = default);

    Task<ReactionHistoryPage> GetPageByDeviceIdAsync(
        string deviceId,
        int skip,
        int take,
        DateTimeOffset? from = null,
        DateTimeOffset? toInclusive = null,
        CancellationToken cancellationToken = default);
}

public sealed record ReactionHistoryPage(IReadOnlyList<ReactionLog> Items, bool HasNextPage);
