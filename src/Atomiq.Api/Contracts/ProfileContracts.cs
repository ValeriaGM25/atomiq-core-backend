namespace Atomiq.Api.Contracts;

public sealed record ProfileResponse(
    string DeviceId,
    int CurrentLevel,
    int TotalXp,
    IReadOnlyCollection<string> DiscoveredReactions);

public sealed record AddXpRequest(string? DeviceId, string? EventId, int XpGained);

public sealed record AddXpResponse(
    string DeviceId,
    int CurrentLevel,
    int TotalXp,
    IReadOnlyCollection<string> DiscoveredReactions,
    bool Applied,
    bool DuplicateEvent);
