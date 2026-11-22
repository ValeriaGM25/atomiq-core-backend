using Atomiq.Domain;

namespace Atomiq.Application.Reactions;

internal static class ReactionMappings
{
    public static ReactionLogDto ToDto(ReactionLog reaction)
    {
        return new ReactionLogDto(
            reaction.Id,
            reaction.EventId,
            reaction.DeviceId,
            reaction.ProfileId,
            reaction.ReactantA,
            reaction.ReactantB,
            reaction.ProductFormula,
            reaction.ReactionEquation,
            reaction.DeltaEnthalpyKjMol,
            reaction.ElectronsTransferred,
            reaction.IsValid,
            reaction.OutcomeType,
            reaction.ThermodynamicClassification,
            reaction.TutorExplanation,
            reaction.ExplanationSource,
            reaction.Timestamp);
    }
}
