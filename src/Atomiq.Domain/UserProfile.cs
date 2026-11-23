namespace Atomiq.Domain;

public sealed class UserProfile
{
    private readonly HashSet<string> discoveredReactions;
    private readonly HashSet<string> appliedXpEventIds;

    private UserProfile(
        string id,
        string deviceId,
        string? username,
        int currentLevel,
        int totalXp,
        IEnumerable<string> discoveredReactions,
        IEnumerable<string> appliedXpEventIds,
        DateTimeOffset createdAt,
        DateTimeOffset lastActiveAt)
    {
        Id = RequireIdentifier(id, nameof(id));
        DeviceId = RequireIdentifier(deviceId, nameof(deviceId));
        Username = NormalizeOptional(username);
        CurrentLevel = currentLevel >= 0
            ? currentLevel
            : throw new DomainException("Current level cannot be negative.");
        TotalXp = totalXp >= 0
            ? totalXp
            : throw new DomainException("Total XP cannot be negative.");
        this.discoveredReactions = NormalizeIdentifiers(discoveredReactions, nameof(discoveredReactions));
        this.appliedXpEventIds = NormalizeIdentifiers(appliedXpEventIds, nameof(appliedXpEventIds));
        CreatedAt = EnsureUtc(createdAt);
        LastActiveAt = EnsureUtc(lastActiveAt);
    }

    public string Id { get; }
    public string DeviceId { get; }
    public string? Username { get; private set; }
    public int CurrentLevel { get; private set; }
    public int TotalXp { get; private set; }
    public int ReactionsDiscoveredCount => discoveredReactions.Count;
    public IReadOnlyCollection<string> DiscoveredReactions => discoveredReactions.ToArray();
    public IReadOnlyCollection<string> AppliedXpEventIds => appliedXpEventIds.ToArray();
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastActiveAt { get; private set; }

    public static UserProfile Create(string deviceId, string? username = null, DateTimeOffset? now = null)
    {
        var timestamp = EnsureUtc(now ?? DateTimeOffset.UtcNow);
        return new UserProfile(
            Guid.NewGuid().ToString("N"),
            deviceId,
            username,
            0,
            0,
            [],
            [],
            timestamp,
            timestamp);
    }

    public static UserProfile Rehydrate(
        string id,
        string deviceId,
        string? username,
        int currentLevel,
        int totalXp,
        IEnumerable<string> discoveredReactions,
        IEnumerable<string> appliedXpEventIds,
        DateTimeOffset createdAt,
        DateTimeOffset lastActiveAt)
    {
        return new UserProfile(
            id,
            deviceId,
            username,
            currentLevel,
            totalXp,
            discoveredReactions,
            appliedXpEventIds,
            createdAt,
            lastActiveAt);
    }

    public void SetUsername(string? username)
    {
        Username = NormalizeOptional(username);
        Touch();
    }

    public bool DiscoverReaction(string reactionId)
    {
        var normalized = RequireIdentifier(reactionId, nameof(reactionId));
        var added = discoveredReactions.Add(normalized);
        Touch();
        return added;
    }

    public bool ApplyXpEvent(string eventId, int amount, DateTimeOffset? now = null)
    {
        var normalizedEventId = RequireIdentifier(eventId, nameof(eventId));
        if (amount < 0)
        {
            throw new DomainException("XP amount cannot be negative.");
        }

        if (!appliedXpEventIds.Add(normalizedEventId))
        {
            Touch(now);
            return false;
        }

        try
        {
            TotalXp = checked(TotalXp + amount);
        }
        catch (OverflowException)
        {
            appliedXpEventIds.Remove(normalizedEventId);
            throw new DomainException("Total XP exceeds the supported limit.");
        }

        Touch(now);
        return true;
    }

    public void Touch(DateTimeOffset? now = null)
    {
        LastActiveAt = EnsureUtc(now ?? DateTimeOffset.UtcNow);
    }

    private static HashSet<string> NormalizeIdentifiers(IEnumerable<string> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            result.Add(RequireIdentifier(value, name));
        }

        return result;
    }

    private static string RequireIdentifier(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime();
    }
}
