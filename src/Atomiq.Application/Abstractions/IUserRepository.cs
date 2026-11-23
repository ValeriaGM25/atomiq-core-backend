using Atomiq.Domain;

namespace Atomiq.Application.Abstractions;

public interface IUserRepository
{
    Task<UserProfile?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);

    Task<UserProfile?> TryCreateAsync(UserProfile profile, CancellationToken cancellationToken = default);

    Task<XpUpdateResult> AddXpAsync(
        string deviceId,
        string eventId,
        int amount,
        CancellationToken cancellationToken = default);
}

public enum XpUpdateStatus
{
    Applied,
    DuplicateEvent,
    ProfileNotFound
}

public sealed record XpUpdateResult(XpUpdateStatus Status, UserProfile? Profile);
