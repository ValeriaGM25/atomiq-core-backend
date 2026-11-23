using MongoDB.Driver;
using MongoDB.Bson;

namespace Atomiq.Infrastructure.Mongo;

public sealed class MongoIndexInitializer(MongoDbContext context)
{
    private static readonly IReadOnlyList<MongoIndexDefinition> ExpectedIndexes =
    [
        new(
            MongoDbContext.ProfilesCollectionName,
            "ux_profiles_deviceId",
            [new("deviceId", 1)],
            true),
        new(
            MongoDbContext.ReactionLogsCollectionName,
            "ux_reaction_logs_eventId",
            [new("eventId", 1)],
            true),
        new(
            MongoDbContext.ReactionLogsCollectionName,
            "ix_reaction_logs_deviceId_timestamp",
            [new("deviceId", 1), new("timestamp", -1)],
            false),
        new(
            MongoDbContext.ReactionLogsCollectionName,
            "ix_reaction_logs_reactantA_reactantB",
            [new("reactantA", 1), new("reactantB", 1)],
            false)
    ];

    public static IReadOnlyList<MongoIndexDefinition> RequiredIndexes => ExpectedIndexes;

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

    public async Task<MongoIndexReadinessResult> VerifyIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profileIndexes = await context.Profiles.Indexes.ListAsync(cancellationToken);
            var reactionIndexes = await context.ReactionLogs.Indexes.ListAsync(cancellationToken);
            var actualIndexes = (await profileIndexes.ToListAsync(cancellationToken))
                .Concat(await reactionIndexes.ToListAsync(cancellationToken))
                .ToArray();
            var missingIndexes = ExpectedIndexes
                .Where(expected => !actualIndexes.Any(actual => Matches(actual, expected)))
                .Select(index => index.Name)
                .ToArray();

            return missingIndexes.Length == 0
                ? MongoIndexReadinessResult.Ready
                : MongoIndexReadinessResult.Missing(missingIndexes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MongoException)
        {
            return MongoIndexReadinessResult.Unavailable;
        }
        catch (TimeoutException)
        {
            return MongoIndexReadinessResult.Unavailable;
        }
    }

    private static bool Matches(BsonDocument actual, MongoIndexDefinition expected)
    {
        if (!actual.TryGetValue("name", out var name)
            || !string.Equals(name.AsString, expected.Name, StringComparison.Ordinal)
            || !actual.TryGetValue("key", out var key)
            || !key.IsBsonDocument)
        {
            return false;
        }

        var actualKey = key.AsBsonDocument.Elements.ToArray();
        if (actualKey.Length != expected.Fields.Count)
        {
            return false;
        }

        for (var index = 0; index < actualKey.Length; index++)
        {
            var expectedField = expected.Fields[index];
            var actualField = actualKey[index];
            if (!string.Equals(actualField.Name, expectedField.Name, StringComparison.Ordinal)
                || actualField.Value.ToInt32() != expectedField.Direction)
            {
                return false;
            }
        }

        return expected.Unique == actual.GetValue("unique", false).ToBoolean();
    }
}

public sealed record MongoIndexField(string Name, int Direction);

public sealed record MongoIndexDefinition(
    string CollectionName,
    string Name,
    IReadOnlyList<MongoIndexField> Fields,
    bool Unique);

public enum MongoIndexReadinessStatus
{
    Ready,
    MissingIndexes,
    Unavailable
}

public sealed record MongoIndexReadinessResult(
    MongoIndexReadinessStatus Status,
    IReadOnlyList<string> MissingIndexNames)
{
    public static MongoIndexReadinessResult Ready { get; } =
        new(MongoIndexReadinessStatus.Ready, []);

    public static MongoIndexReadinessResult Unavailable { get; } =
        new(MongoIndexReadinessStatus.Unavailable, []);

    public static MongoIndexReadinessResult Missing(IReadOnlyList<string> names) =>
        new(MongoIndexReadinessStatus.MissingIndexes, names);
}
