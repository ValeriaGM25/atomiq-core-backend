using Atomiq.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Atomiq.Infrastructure.Mongo;

public sealed class MongoDbContext
{
    public const string ProfilesCollectionName = "profiles";
    public const string ReactionLogsCollectionName = "reaction_logs";

    private readonly IMongoDatabase database;

    public MongoDbContext(IOptions<MongoSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                "MongoDB is not configured. Set Mongo:ConnectionString through local configuration or User Secrets.");
        }

        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
        {
            throw new InvalidOperationException("Mongo:DatabaseName must be configured.");
        }

        var client = new MongoClient(settings.ConnectionString);
        database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<UserProfileDocument> Profiles =>
        database.GetCollection<UserProfileDocument>(ProfilesCollectionName);

    public IMongoCollection<ReactionLogDocument> ReactionLogs =>
        database.GetCollection<ReactionLogDocument>(ReactionLogsCollectionName);
}
