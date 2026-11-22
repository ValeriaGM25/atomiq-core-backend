using Atomiq.Application.Abstractions;
using Atomiq.Domain;
using MongoDB.Driver;

namespace Atomiq.Infrastructure.Mongo;

public sealed class MongoReactionHistoryRepository(MongoDbContext context) : IReactionHistoryRepository
{
    public async Task<ReactionLog?> TryCreateAsync(
        ReactionLog reaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        try
        {
            await context.ReactionLogs.InsertOneAsync(
                MongoMappings.ToDocument(reaction),
                cancellationToken: cancellationToken);
            return reaction;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 11000)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ReactionLog>> GetByDeviceIdAsync(
        string deviceId,
        DateTimeOffset? from = null,
        DateTimeOffset? toInclusive = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainException("deviceId is required.");
        }

        var filterBuilder = Builders<ReactionLogDocument>.Filter;
        var filter = filterBuilder.Eq(reaction => reaction.DeviceId, deviceId.Trim());
        if (from is not null)
        {
            filter &= filterBuilder.Gte(reaction => reaction.Timestamp, from.Value.ToUniversalTime());
        }

        if (toInclusive is not null)
        {
            filter &= filterBuilder.Lte(reaction => reaction.Timestamp, toInclusive.Value.ToUniversalTime());
        }

        var documents = await context.ReactionLogs
            .Find(filter)
            .SortByDescending(reaction => reaction.Timestamp)
            .ToListAsync(cancellationToken);

        return documents.Select(MongoMappings.ToDomain).ToArray();
    }
}
