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

    public async Task<ReactionHistoryPage> GetPageByDeviceIdAsync(
        string deviceId,
        int skip,
        int take,
        DateTimeOffset? from = null,
        DateTimeOffset? toInclusive = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainException("deviceId is required.");
        }

        if (skip < 0 || take is < 1 or > 100)
        {
            throw new DomainException("History pagination limits are invalid.");
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
            .Skip(skip)
            .Limit(take + 1)
            .ToListAsync(cancellationToken);

        var hasNextPage = documents.Count > take;
        var items = documents
            .Take(take)
            .Select(MongoMappings.ToDomain)
            .ToArray();

        return new ReactionHistoryPage(items, hasNextPage);
    }
}
