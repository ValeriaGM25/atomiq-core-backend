using Atomiq.Domain;

namespace Atomiq.Application.Tutor;

public enum UnityReactionOutcomeType
{
    None = 0,
    IonicCrystalBond = 1,
    ExothermicAlkaliExplosion = 2,
    ElectrostaticRepulsion = 3
}

public static class UnityReactionOutcomeMapper
{
    public static bool TryMap(
        int value,
        out UnityReactionOutcomeType unityOutcome,
        out ReactionOutcomeType domainOutcome)
    {
        switch (value)
        {
            case (int)UnityReactionOutcomeType.None:
                unityOutcome = UnityReactionOutcomeType.None;
                domainOutcome = ReactionOutcomeType.Unknown;
                return true;
            case (int)UnityReactionOutcomeType.IonicCrystalBond:
                unityOutcome = UnityReactionOutcomeType.IonicCrystalBond;
                domainOutcome = ReactionOutcomeType.Valid;
                return true;
            case (int)UnityReactionOutcomeType.ExothermicAlkaliExplosion:
                unityOutcome = UnityReactionOutcomeType.ExothermicAlkaliExplosion;
                domainOutcome = ReactionOutcomeType.Valid;
                return true;
            case (int)UnityReactionOutcomeType.ElectrostaticRepulsion:
                unityOutcome = UnityReactionOutcomeType.ElectrostaticRepulsion;
                domainOutcome = ReactionOutcomeType.NoReaction;
                return true;
            default:
                unityOutcome = default;
                domainOutcome = default;
                return false;
        }
    }
}
