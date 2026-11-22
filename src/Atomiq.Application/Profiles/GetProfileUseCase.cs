using Atomiq.Application.Abstractions;

namespace Atomiq.Application.Profiles;

public sealed class GetProfileUseCase(IUserRepository users)
{
    public async Task<ProfileDto?> ExecuteAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var profile = await users.GetByDeviceIdAsync(deviceId, cancellationToken);
        return profile is null ? null : ProfileMappings.ToDto(profile);
    }
}
