using Atomiq.Domain;

namespace Atomiq.Application.Profiles;

internal static class ProfileMappings
{
    public static ProfileDto ToDto(UserProfile profile)
    {
        return new ProfileDto(
            profile.Id,
            profile.DeviceId,
            profile.Username,
            profile.CurrentLevel,
            profile.TotalXp,
            profile.ReactionsDiscoveredCount,
            profile.DiscoveredReactions.ToArray(),
            profile.CreatedAt,
            profile.LastActiveAt);
    }
}
