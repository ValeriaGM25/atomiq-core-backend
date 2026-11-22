using Atomiq.Application.Abstractions;
using Atomiq.Application.Profiles;
using Atomiq.Application.Reactions;
using Atomiq.Domain;

namespace Atomiq.Application.Tests;

public sealed class ApplicationUseCaseTests
{
    [Fact]
    public async Task GetOrCreateReturnsOneProfileForConcurrentRequests()
    {
        var repository = new InMemoryUserRepository();
        var useCase = new GetOrCreateProfileUseCase(repository);

        var profiles = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => useCase.ExecuteAsync("device-1")));

        Assert.All(profiles, profile => Assert.Equal(profiles[0].Id, profile.Id));
    }

    [Fact]
    public async Task AddXpIsIdempotentByEventId()
    {
        var repository = new InMemoryUserRepository();
        var profile = await new GetOrCreateProfileUseCase(repository).ExecuteAsync("device-1");
        var useCase = new AddXpUseCase(repository);

        var first = await useCase.ExecuteAsync(profile.DeviceId, "event-1", 10);
        var second = await useCase.ExecuteAsync(profile.DeviceId, "event-1", 10);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.True(first.Applied);
        Assert.True(second.DuplicateEvent);
        Assert.Equal(10, second.Profile.TotalXp);
    }

    [Fact]
    public async Task NegativeXpIsRejectedBeforeRepositoryCall()
    {
        var repository = new InMemoryUserRepository();
        var useCase = new AddXpUseCase(repository);

        await Assert.ThrowsAsync<ApplicationException>(() =>
            useCase.ExecuteAsync("device-1", "event-1", -1));
    }

    [Fact]
    public async Task ReactionHistoryDoesNotPersistDuplicateEvent()
    {
        var repository = new InMemoryReactionHistoryRepository();
        var useCase = new SaveReactionLogUseCase(repository);
        var reaction = CreateReaction();

        var first = await useCase.ExecuteAsync(reaction);
        var second = await useCase.ExecuteAsync(reaction);
        var history = await new GetReactionHistoryUseCase(repository).ExecuteAsync("device-1");

        Assert.True(first.Inserted);
        Assert.True(second.DuplicateEvent);
        Assert.Single(history);
    }

    [Fact]
    public async Task CancellationTokenIsForwarded()
    {
        var repository = new InMemoryUserRepository();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new GetProfileUseCase(repository).ExecuteAsync("device-1", source.Token));
    }

    private static ReactionLog CreateReaction() => ReactionLog.Create(
        "event-1",
        "device-1",
        "profile-1",
        "H2",
        "O2",
        "H2O",
        "2H2+O2->2H2O",
        -285.8,
        0,
        true,
        ReactionOutcomeType.Valid,
        "Exothermic",
        null,
        null);

    private sealed class InMemoryUserRepository : IUserRepository
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

        public Task<XpUpdateResult> AddXpAsync(string deviceId, string eventId, int amount, CancellationToken cancellationToken = default)
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

    private sealed class InMemoryReactionHistoryRepository : IReactionHistoryRepository
    {
        private readonly Dictionary<string, ReactionLog> reactions = new(StringComparer.Ordinal);

        public Task<ReactionLog?> TryCreateAsync(ReactionLog reaction, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(reactions.TryAdd(reaction.EventId, reaction) ? reaction : null);
        }

        public Task<IReadOnlyList<ReactionLog>> GetByDeviceIdAsync(
            string deviceId,
            DateTimeOffset? from = null,
            DateTimeOffset? toInclusive = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = reactions.Values
                .Where(reaction => reaction.DeviceId == deviceId)
                .Where(reaction => from is null || reaction.Timestamp >= from.Value)
                .Where(reaction => toInclusive is null || reaction.Timestamp <= toInclusive.Value)
                .ToArray();
            return Task.FromResult<IReadOnlyList<ReactionLog>>(result);
        }
    }
}
