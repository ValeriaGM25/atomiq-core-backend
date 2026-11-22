using MongoDB.Driver;

namespace Atomiq.Infrastructure.Mongo;

public sealed class MongoIndexInitializer(MongoDbContext context)
{
    // Explicitly invoked by an operator or deployment step; never run at application startup.
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var profileIndexes = new[]
        {
            new CreateIndexModel<UserProfileDocument>(
                Builders<UserProfileDocument>.IndexKeys.Ascending(profile => profile.DeviceId),
                new CreateIndexOptions { Name = "ux_profiles_deviceId", Unique = true })
        };

        var reactionIndexes = new[]
        {
            new CreateIndexModel<ReactionLogDocument>(
                Builders<ReactionLogDocument>.IndexKeys.Ascending(reaction => reaction.EventId),
                new CreateIndexOptions { Name = "ux_reaction_logs_eventId", Unique = true }),
            new CreateIndexModel<ReactionLogDocument>(
                Builders<ReactionLogDocument>.IndexKeys
                    .Ascending(reaction => reaction.DeviceId)
                    .Descending(reaction => reaction.Timestamp),
                new CreateIndexOptions { Name = "ix_reaction_logs_deviceId_timestamp" }),
            new CreateIndexModel<ReactionLogDocument>(
                Builders<ReactionLogDocument>.IndexKeys
                    .Ascending(reaction => reaction.ReactantA)
                    .Ascending(reaction => reaction.ReactantB),
                new CreateIndexOptions { Name = "ix_reaction_logs_reactantA_reactantB" })
        };

        await context.Profiles.Indexes.CreateManyAsync(profileIndexes, cancellationToken);
        await context.ReactionLogs.Indexes.CreateManyAsync(reactionIndexes, cancellationToken);
    }
}
