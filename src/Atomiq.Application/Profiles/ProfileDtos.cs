namespace Atomiq.Application.Profiles;

public sealed record ProfileDto(
    string Id,
    string DeviceId,
    string? Username,
    int CurrentLevel,
    int TotalXp,
    int ReactionsDiscoveredCount,
    IReadOnlyCollection<string> DiscoveredReactions,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActiveAt);

public sealed record AddXpResult(ProfileDto Profile, bool Applied, bool DuplicateEvent);
