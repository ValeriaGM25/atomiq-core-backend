using Atomiq.Domain;

namespace Atomiq.Domain.Tests;

public sealed class DomainTests
{
    [Fact]
    public void DomainMarkerBelongsToDomainAssembly()
    {
        Assert.Equal("Atomiq.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void NewProfileStartsWithZeroXpAndUtcDates()
    {
        var profile = UserProfile.Create(" device-1 ", " Ada ");

        Assert.Equal("device-1", profile.DeviceId);
        Assert.Equal("Ada", profile.Username);
        Assert.Equal(0, profile.TotalXp);
        Assert.Equal(TimeSpan.Zero, profile.CreatedAt.Offset);
        Assert.Equal(TimeSpan.Zero, profile.LastActiveAt.Offset);
    }

    [Fact]
    public void ApplyingTheSameXpEventTwiceOnlyChangesXpOnce()
    {
        var profile = UserProfile.Create("device-1");

        Assert.True(profile.ApplyXpEvent("event-1", 25));
        Assert.False(profile.ApplyXpEvent("event-1", 25));
        Assert.Equal(25, profile.TotalXp);
    }

    [Fact]
    public void NegativeXpIsRejected()
    {
        var profile = UserProfile.Create("device-1");

        Assert.Throws<DomainException>(() => profile.ApplyXpEvent("event-1", -1));
    }

    [Fact]
    public void DiscoveredReactionsAreUniqueAndReadOnly()
    {
        var profile = UserProfile.Create("device-1");

        Assert.True(profile.DiscoverReaction("reaction-1"));
        Assert.False(profile.DiscoverReaction(" reaction-1 "));
        Assert.Equal(1, profile.ReactionsDiscoveredCount);
        Assert.IsAssignableFrom<IReadOnlyCollection<string>>(profile.DiscoveredReactions);
    }

    [Fact]
    public void XpOverflowIsRejectedWithoutRecordingTheEvent()
    {
        var profile = UserProfile.Rehydrate(
            "profile-1",
            "device-1",
            null,
            0,
            int.MaxValue,
            [],
            [],
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => profile.ApplyXpEvent("event-1", 1));
        Assert.DoesNotContain("event-1", profile.AppliedXpEventIds);
    }

    [Fact]
    public void ReactionLogNormalizesFormulaWhitespaceAndKeepsUtcTimestamp()
    {
        var reaction = ReactionLog.Create(
            "event-1",
            "device-1",
            "profile-1",
            " H2 ",
            " O2 ",
            " H2O ",
            "2 H2 + O2 -> 2 H2O",
            -285.8,
            0,
            true,
            ReactionOutcomeType.Valid,
            "Exothermic",
            null,
            null,
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(-5)));

        Assert.Equal("H2", reaction.ReactantA);
        Assert.Equal("O2", reaction.ReactantB);
        Assert.Equal("H2O", reaction.ProductFormula);
        Assert.Equal(TimeSpan.Zero, reaction.Timestamp.Offset);
    }
}
