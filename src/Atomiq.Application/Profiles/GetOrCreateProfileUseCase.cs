using Atomiq.Application.Abstractions;
using Atomiq.Domain;

namespace Atomiq.Application.Profiles;

public sealed class GetOrCreateProfileUseCase(IUserRepository users)
{
    public async Task<ProfileDto> ExecuteAsync(
        string deviceId,
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await users.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (existing is not null)
        {
            return ProfileMappings.ToDto(existing);
        }

        var created = UserProfile.Create(deviceId, username);
        var inserted = await users.TryCreateAsync(created, cancellationToken);
        if (inserted is not null)
        {
            return ProfileMappings.ToDto(inserted);
        }

        var concurrentProfile = await users.GetByDeviceIdAsync(deviceId, cancellationToken);
        return concurrentProfile is null
            ? throw new ApplicationException("The profile could not be created or retrieved.")
            : ProfileMappings.ToDto(concurrentProfile);
    }
}
