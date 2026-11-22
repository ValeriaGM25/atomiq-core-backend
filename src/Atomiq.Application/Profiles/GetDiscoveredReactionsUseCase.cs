using Atomiq.Application.Abstractions;

namespace Atomiq.Application.Profiles;

public sealed class GetDiscoveredReactionsUseCase(IUserRepository users)
{
    public async Task<IReadOnlyCollection<string>?> ExecuteAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var profile = await users.GetByDeviceIdAsync(deviceId, cancellationToken);
        return profile?.DiscoveredReactions.ToArray();
    }
}
