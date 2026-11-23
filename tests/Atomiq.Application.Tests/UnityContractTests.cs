using Atomiq.Application.Tutor;
using Atomiq.Domain;

namespace Atomiq.Application.Tests;

public sealed class UnityContractTests
{
    [Theory]
    [InlineData(0, UnityReactionOutcomeType.None, ReactionOutcomeType.Unknown)]
    [InlineData(1, UnityReactionOutcomeType.IonicCrystalBond, ReactionOutcomeType.Valid)]
    [InlineData(2, UnityReactionOutcomeType.ExothermicAlkaliExplosion, ReactionOutcomeType.Valid)]
    [InlineData(3, UnityReactionOutcomeType.ElectrostaticRepulsion, ReactionOutcomeType.NoReaction)]
    public void UnityOutcomeMapsExplicitlyWithoutDerivingIsValid(
        int value,
        UnityReactionOutcomeType expectedUnityOutcome,
        ReactionOutcomeType expectedDomainOutcome)
    {
        var mapped = UnityReactionOutcomeMapper.TryMap(value, out var unityOutcome, out var domainOutcome);

        Assert.True(mapped);
        Assert.Equal(expectedUnityOutcome, unityOutcome);
        Assert.Equal(expectedDomainOutcome, domainOutcome);
        Assert.NotEqual(ReactionOutcomeType.Invalid, domainOutcome);
    }

    [Fact]
    public void UnknownUnityOutcomeIsRejected()
    {
        var mapped = UnityReactionOutcomeMapper.TryMap(99, out _, out _);

        Assert.False(mapped);
    }
}
