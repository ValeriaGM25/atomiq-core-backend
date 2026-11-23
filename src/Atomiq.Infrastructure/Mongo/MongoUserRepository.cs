using Atomiq.Application.Abstractions;
using Atomiq.Domain;
using MongoDB.Driver;

namespace Atomiq.Infrastructure.Mongo;

public sealed class MongoUserRepository(MongoDbContext context) : IUserRepository
{
    public async Task<UserProfile?> GetByDeviceIdAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeviceId = RequireIdentifier(deviceId, nameof(deviceId));
        var document = await context.Profiles
            .Find(Builders<UserProfileDocument>.Filter.Eq(profile => profile.DeviceId, normalizedDeviceId))
            .SingleOrDefaultAsync(cancellationToken);

        return document is null ? null : MongoMappings.ToDomain(document);
    }

    public async Task<UserProfile?> TryCreateAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        try
        {
            await context.Profiles.InsertOneAsync(
                MongoMappings.ToDocument(profile),
                cancellationToken: cancellationToken);
            return profile;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 11000)
        {
            return null;
        }
    }

    public async Task<XpUpdateResult> AddXpAsync(
        string deviceId,
        string eventId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        var normalizedDeviceId = RequireIdentifier(deviceId, nameof(deviceId));
        var normalizedEventId = RequireIdentifier(eventId, nameof(eventId));
        if (amount < 0)
        {
            throw new DomainException("XP amount cannot be negative.");
        }

        var filterBuilder = Builders<UserProfileDocument>.Filter;
        var filter = filterBuilder.Eq(profile => profile.DeviceId, normalizedDeviceId)
            & !filterBuilder.AnyEq(profile => profile.AppliedXpEventIds, normalizedEventId)
            & filterBuilder.Lte(profile => profile.TotalXp, int.MaxValue - amount);
        var update = Builders<UserProfileDocument>.Update
            .Inc(profile => profile.TotalXp, amount)
            .AddToSet(profile => profile.AppliedXpEventIds, normalizedEventId)
            .Set(profile => profile.LastActiveAt, DateTimeOffset.UtcNow);

        var updated = await context.Profiles.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<UserProfileDocument>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken);

        if (updated is not null)
        {
            return new XpUpdateResult(XpUpdateStatus.Applied, MongoMappings.ToDomain(updated));
        }

        var current = await GetByDeviceIdAsync(normalizedDeviceId, cancellationToken);
        if (current is null)
        {
            return new XpUpdateResult(XpUpdateStatus.ProfileNotFound, null);
        }

        if (current.AppliedXpEventIds.Contains(normalizedEventId, StringComparer.Ordinal))
        {
            return new XpUpdateResult(XpUpdateStatus.DuplicateEvent, current);
        }

        throw new DomainException("Total XP exceeds the supported limit.");
    }

    private static string RequireIdentifier(string? value, string name)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new DomainException($"{name} is required.")
            : value.Trim();
    }
}
