using Atomiq.Domain;
using Atomiq.Application.Abstractions;
using Atomiq.Infrastructure.Mongo;
using Atomiq.Infrastructure.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Atomiq.Infrastructure.IntegrationTests;

[Collection("MongoDB integration")]
public sealed class MongoRepositoryIntegrationTests(MongoIntegrationFixture fixture)
{
    [Fact]
    public async Task LocalMongoConnectionIsRealAndAvailable()
    {
        var result = await fixture.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        Assert.Equal(1, result["ok"].ToInt32());
    }

    [Fact]
    public async Task ProfileIsCreatedAndReadBack()
    {
        var profile = UserProfile.Create("profile-" + Guid.NewGuid().ToString("N"), " Test User ",
            DateTimeOffset.Parse("2026-01-02T03:04:05.006+02:00", CultureInfo.InvariantCulture).ToUniversalTime());

        var created = await fixture.Users.TryCreateAsync(profile);
        var loaded = await fixture.Users.GetByDeviceIdAsync(profile.DeviceId);

        Assert.Same(profile, created);
        Assert.NotNull(loaded);
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal("Test User", loaded.Username);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.UtcDateTime.Kind);
        Assert.Equal(profile.CreatedAt, loaded.CreatedAt);
    }

    [Fact]
    public async Task UniqueDeviceIdIndexRejectsConcurrentProfileCreation()
    {
        var deviceId = "device-" + Guid.NewGuid().ToString("N");
        var profiles = Enumerable.Range(0, 16)
            .Select(_ => UserProfile.Create(deviceId))
            .ToArray();

        var results = await Task.WhenAll(profiles.Select(profile => fixture.Users.TryCreateAsync(profile)));

        Assert.Equal(1, results.Count(result => result is not null));
        Assert.Equal(15, results.Count(result => result is null));
    }

    [Fact]
    public async Task ConcurrentXpUpdatesAreAtomic()
    {
        var profile = await CreateProfileAsync();
        var updates = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(index => fixture.Users.AddXpAsync(profile.DeviceId, "xp-" + index, 3)));
        var loaded = await fixture.Users.GetByDeviceIdAsync(profile.DeviceId);

        Assert.All(updates, result => Assert.Equal(XpUpdateStatus.Applied, result.Status));
        Assert.Equal(60, loaded!.TotalXp);
        Assert.Equal(20, loaded.AppliedXpEventIds.Count);
    }

    [Fact]
    public async Task RepeatingXpEventIsIdempotent()
    {
        var profile = await CreateProfileAsync();

        var first = await fixture.Users.AddXpAsync(profile.DeviceId, "same-xp-event", 10);
        var second = await fixture.Users.AddXpAsync(profile.DeviceId, "same-xp-event", 10);

        Assert.Equal(XpUpdateStatus.Applied, first.Status);
        Assert.Equal(XpUpdateStatus.DuplicateEvent, second.Status);
        Assert.Equal(10, (await fixture.Users.GetByDeviceIdAsync(profile.DeviceId))!.TotalXp);
    }

    [Fact]
    public async Task ConcurrentRepeatedXpEventAppliesOnce()
    {
        var profile = await CreateProfileAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => fixture.Users.AddXpAsync(profile.DeviceId, "concurrent-xp-event", 7)));

        Assert.Single(results, result => result.Status == XpUpdateStatus.Applied);
        Assert.Equal(19, results.Count(result => result.Status == XpUpdateStatus.DuplicateEvent));
        Assert.Equal(7, (await fixture.Users.GetByDeviceIdAsync(profile.DeviceId))!.TotalXp);
    }

    [Fact]
    public async Task XpOverflowIsRejectedWithoutPersistingEvent()
    {
        var profile = UserProfile.Rehydrate(
            Guid.NewGuid().ToString("N"),
            "overflow-" + Guid.NewGuid().ToString("N"),
            null,
            0,
            int.MaxValue,
            [],
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        await fixture.Users.TryCreateAsync(profile);

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Users.AddXpAsync(profile.DeviceId, "overflow-event", 1));
        var loaded = await fixture.Users.GetByDeviceIdAsync(profile.DeviceId);

        Assert.Equal(int.MaxValue, loaded!.TotalXp);
        Assert.DoesNotContain("overflow-event", loaded.AppliedXpEventIds);
    }

    [Fact]
    public async Task ReactionLogIsSavedAndRehydratedWithUtcValues()
    {
        var profile = await CreateProfileAsync();
        var timestamp = DateTimeOffset.Parse("2026-02-03T04:05:06.007-05:00", CultureInfo.InvariantCulture);
        var reaction = CreateReaction("reaction-save", profile, timestamp);

        var saved = await fixture.Reactions.TryCreateAsync(reaction);
        var page = await fixture.Reactions.GetPageByDeviceIdAsync(profile.DeviceId, 0, 10);

        Assert.Same(reaction, saved);
        var loaded = Assert.Single(page.Items);
        Assert.Equal(reaction.EventId, loaded.EventId);
        Assert.Equal(timestamp.ToUniversalTime(), loaded.Timestamp);
        Assert.Equal(ReactionOutcomeType.Valid, loaded.OutcomeType);
    }

    [Fact]
    public async Task DuplicateReactionEventIsRejectedButDifferentEventsWithSameContentAreStored()
    {
        var profile = await CreateProfileAsync();
        var first = CreateReaction("reaction-duplicate", profile, DateTimeOffset.UtcNow);
        var sameEvent = CreateReaction("reaction-duplicate", profile, DateTimeOffset.UtcNow.AddSeconds(1));
        var differentEvent = CreateReaction("reaction-new", profile, first.Timestamp);

        Assert.NotNull(await fixture.Reactions.TryCreateAsync(first));
        Assert.Null(await fixture.Reactions.TryCreateAsync(sameEvent));
        Assert.NotNull(await fixture.Reactions.TryCreateAsync(differentEvent));

        var page = await fixture.Reactions.GetPageByDeviceIdAsync(profile.DeviceId, 0, 10);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task HistoryIsOrderedAndPagedByTimestamp()
    {
        var profile = await CreateProfileAsync();
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        foreach (var index in Enumerable.Range(0, 5))
        {
            await fixture.Reactions.TryCreateAsync(
                CreateReaction("history-" + index, profile, start.AddMinutes(index)));
        }

        var firstPage = await fixture.Reactions.GetPageByDeviceIdAsync(profile.DeviceId, 0, 2);
        var secondPage = await fixture.Reactions.GetPageByDeviceIdAsync(profile.DeviceId, 2, 2);

        Assert.True(firstPage.HasNextPage);
        Assert.Equal(["history-4", "history-3"], firstPage.Items.Select(item => item.EventId));
        Assert.True(secondPage.HasNextPage);
        Assert.Equal(["history-2", "history-1"], secondPage.Items.Select(item => item.EventId));
    }

    [Fact]
    public async Task HistoryFinalPageReportsNoNextPage()
    {
        var profile = await CreateProfileAsync();
        var start = DateTimeOffset.UtcNow.AddMinutes(-3);
        foreach (var index in Enumerable.Range(0, 3))
        {
            await fixture.Reactions.TryCreateAsync(
                CreateReaction("final-page-" + index, profile, start.AddMinutes(index)));
        }

        var finalPage = await fixture.Reactions.GetPageByDeviceIdAsync(profile.DeviceId, 2, 2);

        Assert.False(finalPage.HasNextPage);
        Assert.Equal(["final-page-0"], finalPage.Items.Select(item => item.EventId));
    }

    [Fact]
    public async Task RawBsonUsesNamedEnumAndUtcDate()
    {
        var profile = await CreateProfileAsync();
        await fixture.Reactions.TryCreateAsync(CreateReaction("serialization", profile, DateTimeOffset.UtcNow));
        var raw = await fixture.Database
            .GetCollection<BsonDocument>(MongoDbContext.ReactionLogsCollectionName)
            .Find(new BsonDocument("eventId", "serialization"))
            .SingleAsync();

        Assert.Equal(BsonType.String, raw["outcomeType"].BsonType);
        Assert.Equal(nameof(ReactionOutcomeType.Valid), raw["outcomeType"].AsString);
        Assert.Equal(BsonType.DateTime, raw["timestamp"].BsonType);
    }

    [Fact]
    public async Task CancellationTokenIsForwardedToMongo()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Users.GetByDeviceIdAsync("cancelled", cancellation.Token));
    }

    [Fact]
    public async Task ConnectionErrorsAreNotHidden()
    {
        var settings = new MongoClientSettings
        {
            ServerSelectionTimeout = TimeSpan.FromMilliseconds(250)
        };
        settings.Servers = [new MongoServerAddress("127.0.0.1", 1)];
        var client = new MongoClient(settings);

        try
        {
            var database = client.GetDatabase("atomiq_connection_error_test");
            await Assert.ThrowsAsync<TimeoutException>(() =>
                database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)));
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task IndexCreationIsIdempotentAndHasExpectedUniqueConstraints()
    {
        await fixture.Indexes.EnsureIndexesAsync();
        var profileIndexes = await fixture.Context.Profiles.Indexes.ListAsync();
        var reactionIndexes = await fixture.Context.ReactionLogs.Indexes.ListAsync();

        var profileIndexDocuments = await profileIndexes.ToListAsync();
        var reactionIndexDocuments = await reactionIndexes.ToListAsync();
        foreach (var expected in MongoIndexInitializer.RequiredIndexes)
        {
            var indexes = expected.CollectionName == MongoDbContext.ProfilesCollectionName
                ? profileIndexDocuments
                : reactionIndexDocuments;
            var actual = indexes.Single(index => index["name"] == expected.Name);

            Assert.Equal(expected.Unique, actual.GetValue("unique", false).ToBoolean());
            Assert.Equal(
                expected.Fields.Select(field => new { field.Name, field.Direction }),
                actual["key"].AsBsonDocument.Elements.Select(field => new
                {
                    field.Name,
                    Direction = field.Value.ToInt32()
                }));
        }
    }

    [Fact]
    public async Task ReadinessDetectsMissingIndexesWithoutCreatingThem()
    {
        using var isolated = CreateIsolatedMongo();
        await isolated.Initializer.EnsureIndexesAsync();
        await isolated.Context.ReactionLogs.Indexes.DropOneAsync("ux_reaction_logs_eventId");

        using var readiness = new MongoIndexReadiness(isolated.Initializer, TimeProvider.System);
        var result = await readiness.GetStatusAsync(forceRefresh: true);
        var indexes = await isolated.Context.ReactionLogs.Indexes.ListAsync();
        var names = (await indexes.ToListAsync()).Select(index => index["name"].AsString).ToArray();

        Assert.Equal(MongoIndexReadinessStatus.MissingIndexes, result.Status);
        Assert.Contains("ux_reaction_logs_eventId", result.MissingIndexNames);
        Assert.DoesNotContain("ux_reaction_logs_eventId", names);
    }

    [Fact]
    public async Task IndexInitializationReportsExistingDuplicatesWithoutDeletingThem()
    {
        using var isolated = CreateIsolatedMongo();
        await isolated.Initializer.EnsureIndexesAsync();
        await isolated.Context.ReactionLogs.Indexes.DropOneAsync("ux_reaction_logs_eventId");

        var profile = UserProfile.Create("duplicate-index-" + Guid.NewGuid().ToString("N"));
        await isolated.Users.TryCreateAsync(profile);
        await isolated.Reactions.TryCreateAsync(CreateReaction("duplicate-event", profile, DateTimeOffset.UtcNow));
        await isolated.Reactions.TryCreateAsync(CreateReaction(
            "duplicate-event", profile, DateTimeOffset.UtcNow.AddSeconds(1)));

        await Assert.ThrowsAsync<MongoCommandException>(() => isolated.Initializer.EnsureIndexesAsync());

        var documents = await isolated.Context.ReactionLogs
            .Find(Builders<ReactionLogDocument>.Filter.Eq(document => document.EventId, "duplicate-event"))
            .ToListAsync();
        Assert.Equal(2, documents.Count);
    }

    [Fact]
    public async Task ReadinessReportsMongoUnavailableWithoutHidingConnectionFailure()
    {
        var databaseName = "atomiq_unavailable_" + Guid.NewGuid().ToString("N");
        var context = new MongoDbContext(Options.Create(new MongoSettings
        {
            ConnectionString = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=250",
            DatabaseName = databaseName
        }));
        var initializer = new MongoIndexInitializer(context);
        using var readiness = new MongoIndexReadiness(initializer, TimeProvider.System);

        var result = await readiness.GetStatusAsync(forceRefresh: true);

        Assert.Equal(MongoIndexReadinessStatus.Unavailable, result.Status);
    }

    private IsolatedMongo CreateIsolatedMongo()
    {
        var databaseName = "atomiq_index_test_" + Guid.NewGuid().ToString("N");
        var client = new MongoClient(fixture.ConnectionString);
        var context = new MongoDbContext(Options.Create(new MongoSettings
        {
            ConnectionString = fixture.ConnectionString,
            DatabaseName = databaseName
        }));
        return new IsolatedMongo(
            client,
            context,
            new MongoIndexInitializer(context),
            new MongoUserRepository(context),
            new MongoReactionHistoryRepository(context));
    }

    private sealed class IsolatedMongo(
        MongoClient client,
        MongoDbContext context,
        MongoIndexInitializer initializer,
        MongoUserRepository users,
        MongoReactionHistoryRepository reactions) : IDisposable
    {
        public MongoDbContext Context { get; } = context;
        public MongoIndexInitializer Initializer { get; } = initializer;
        public MongoUserRepository Users { get; } = users;
        public MongoReactionHistoryRepository Reactions { get; } = reactions;

        public void Dispose() => client.Dispose();
    }

    private async Task<UserProfile> CreateProfileAsync()
    {
        var profile = UserProfile.Create("device-" + Guid.NewGuid().ToString("N"));
        Assert.NotNull(await fixture.Users.TryCreateAsync(profile));
        return profile;
    }

    private static ReactionLog CreateReaction(
        string eventId,
        UserProfile profile,
        DateTimeOffset timestamp)
    {
        return ReactionLog.Create(
            eventId,
            profile.DeviceId,
            profile.Id,
            "Na",
            "Cl",
            "NaCl",
            "Na + Cl -> NaCl",
            -411.2,
            1,
            true,
            ReactionOutcomeType.Valid,
            "Exotermica",
            "Explicacion breve",
            "OfflineFallback",
            timestamp,
            productDisplayName: "Cloruro de sodio");
    }
}

[CollectionDefinition("MongoDB integration", DisableParallelization = true)]
public sealed class MongoIntegrationTestGroup : ICollectionFixture<MongoIntegrationFixture>;
