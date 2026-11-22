using Atomiq.Domain;

namespace Atomiq.Application.Abstractions;

public interface IReactionHistoryRepository
{
    Task<ReactionLog?> TryCreateAsync(
        ReactionLog reaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReactionLog>> GetByDeviceIdAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? toInclusive = null,
        CancellationToken cancellationToken = default);
}
