using Atomiq.Infrastructure.Configuration;
using Atomiq.Infrastructure.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Atomiq.Infrastructure.IntegrationTests;

public sealed class MongoIntegrationFixture : IAsyncLifetime, IDisposable
{
    private readonly MongoClient client;

    public MongoIntegrationFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable("ATOMIQ_MONGO_TEST_CONNECTION")
            ?? "mongodb://127.0.0.1:27017";
        if (!ConnectionString.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase)
            || !IsLoopback(new Uri(ConnectionString).Host))
        {
            throw new InvalidOperationException(
                "ATOMIQ_MONGO_TEST_CONNECTION must point to a local MongoDB instance.");
        }

        DatabaseName = "atomiq_integration_" + Guid.NewGuid().ToString("N");
        client = new MongoClient(ConnectionString);
        Database = client.GetDatabase(DatabaseName);
        Context = new MongoDbContext(Options.Create(new MongoSettings
        {
            ConnectionString = ConnectionString,
            DatabaseName = DatabaseName
        }));
        Users = new MongoUserRepository(Context);
        Reactions = new MongoReactionHistoryRepository(Context);
        Indexes = new MongoIndexInitializer(Context);
    }

    public string ConnectionString { get; }
    public string DatabaseName { get; }
    public IMongoDatabase Database { get; }
    public MongoDbContext Context { get; }
    public MongoUserRepository Users { get; }
    public MongoReactionHistoryRepository Reactions { get; }
    public MongoIndexInitializer Indexes { get; }

    public async Task InitializeAsync()
    {
        await Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await Indexes.EnsureIndexesAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.Profiles.DeleteManyAsync(Builders<UserProfileDocument>.Filter.Empty);
        await Context.ReactionLogs.DeleteManyAsync(Builders<ReactionLogDocument>.Filter.Empty);
    }

    public void Dispose()
    {
        client.Dispose();
    }

    private static bool IsLoopback(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(host, "::1", StringComparison.Ordinal);
    }
}
