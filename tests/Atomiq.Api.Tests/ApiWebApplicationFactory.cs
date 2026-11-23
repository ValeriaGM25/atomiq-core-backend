using System.Globalization;
using Atomiq.Application.Abstractions;
using Atomiq.Domain;
using Atomiq.Infrastructure.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atomiq.Api.Tests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected virtual int PermitLimit => 100;

    public TestUserRepository Users { get; } = new();
    public TestReactionHistoryRepository Reactions { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DeviceTokens:device-1"] = "test-token-1",
                ["Authentication:DeviceTokens:device-2"] = "test-token-2",
                ["Authentication:DeviceTokens:profile-device"] = "profile-token",
                ["Authentication:DeviceTokens:xp-device"] = "xp-token",
                ["Authentication:DeviceTokens:history-device"] = "history-token",
                ["Authentication:DeviceTokens:rate-device"] = "rate-token",
                ["Authentication:DeviceTokens:contract-device"] = "contract-token",
                ["Authentication:DeviceTokens:unity-rest-device"] = "unity-rest-token",
                ["Authentication:DeviceTokens:unity-signalr-device"] = "unity-signalr-token",
                ["RateLimiting:PermitLimit"] = PermitLimit.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:WindowSeconds"] = "60"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.RemoveAll<IReactionHistoryRepository>();
            services.RemoveAll<IMongoIndexReadiness>();
            services.AddSingleton<IUserRepository>(Users);
            services.AddSingleton<IReactionHistoryRepository>(Reactions);
            services.AddSingleton<IMongoIndexReadiness, TestMongoIndexReadiness>();
        });
    }
}

public sealed class TestMongoIndexReadiness : IMongoIndexReadiness
{
    public Task<MongoIndexReadinessResult> GetStatusAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MongoIndexReadinessResult.Ready);
    }

    public void Invalidate()
    {
    }
}

public sealed class MissingMongoIndexReadiness : IMongoIndexReadiness
{
    public Task<MongoIndexReadinessResult> GetStatusAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(MongoIndexReadinessResult.Missing(["ux_reaction_logs_eventId"]));
    }

    public void Invalidate()
    {
    }
}

public sealed class NotReadyApiWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMongoIndexReadiness>();
            services.AddSingleton<IMongoIndexReadiness, MissingMongoIndexReadiness>();
        });
    }
}

public sealed class RateLimitWebApplicationFactory : ApiWebApplicationFactory
{
    protected override int PermitLimit => 1;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }
}

public sealed class TestUserRepository : IUserRepository
{
    private readonly object gate = new();
    private readonly Dictionary<string, UserProfile> profiles = new(StringComparer.Ordinal);

    public Task<UserProfile?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            profiles.TryGetValue(deviceId, out var profile);
            return Task.FromResult(profile);
        }
    }

    public Task<UserProfile?> TryCreateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (profiles.ContainsKey(profile.DeviceId))
            {
                return Task.FromResult<UserProfile?>(null);
            }

            profiles[profile.DeviceId] = profile;
            return Task.FromResult<UserProfile?>(profile);
        }
    }

    public Task<XpUpdateResult> AddXpAsync(
        string deviceId,
        string eventId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!profiles.TryGetValue(deviceId, out var profile))
            {
                return Task.FromResult(new XpUpdateResult(XpUpdateStatus.ProfileNotFound, null));
            }

            var applied = profile.ApplyXpEvent(eventId, amount);
            return Task.FromResult(new XpUpdateResult(
                applied ? XpUpdateStatus.Applied : XpUpdateStatus.DuplicateEvent,
                profile));
        }
    }
}

public sealed class TestReactionHistoryRepository : IReactionHistoryRepository
{
    private readonly object gate = new();
    private readonly Dictionary<string, ReactionLog> reactions = new(StringComparer.Ordinal);

    public Task<ReactionLog?> TryCreateAsync(
        ReactionLog reaction,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(reactions.TryAdd(reaction.EventId, reaction) ? reaction : null);
        }
    }

    public Task<ReactionHistoryPage> GetPageByDeviceIdAsync(
        string deviceId,
        int skip,
        int take,
        DateTimeOffset? from = null,
        DateTimeOffset? toInclusive = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var items = reactions.Values
                .Where(reaction => reaction.DeviceId == deviceId)
                .Where(reaction => from is null || reaction.Timestamp >= from.Value)
                .Where(reaction => toInclusive is null || reaction.Timestamp <= toInclusive.Value)
                .OrderByDescending(reaction => reaction.Timestamp)
                .Skip(skip)
                .Take(take + 1)
                .ToArray();
            return Task.FromResult(new ReactionHistoryPage(
                items.Take(take).ToArray(),
                items.Length > take));
        }
    }

    public void Add(ReactionLog reaction)
    {
        lock (gate)
        {
            reactions[reaction.EventId] = reaction;
        }
    }
}
