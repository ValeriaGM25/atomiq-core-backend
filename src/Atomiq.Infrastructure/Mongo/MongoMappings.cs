using Atomiq.Domain;

namespace Atomiq.Infrastructure.Mongo;

internal static class MongoMappings
{
    public static UserProfileDocument ToDocument(UserProfile profile)
    {
        return new UserProfileDocument
        {
            Id = profile.Id,
            DeviceId = profile.DeviceId,
            Username = profile.Username,
            CurrentLevel = profile.CurrentLevel,
            TotalXp = profile.TotalXp,
            DiscoveredReactions = profile.DiscoveredReactions.ToList(),
            AppliedXpEventIds = profile.AppliedXpEventIds.ToList(),
            CreatedAt = profile.CreatedAt,
            LastActiveAt = profile.LastActiveAt
        };
    }

    public static UserProfile ToDomain(UserProfileDocument document)
    {
        return UserProfile.Rehydrate(
            document.Id,
            document.DeviceId,
            document.Username,
            document.CurrentLevel,
            document.TotalXp,
            document.DiscoveredReactions,
            document.AppliedXpEventIds,
            document.CreatedAt,
            document.LastActiveAt);
    }

    public static ReactionLogDocument ToDocument(ReactionLog reaction)
    {
        return new ReactionLogDocument
        {
            Id = reaction.Id,
            EventId = reaction.EventId,
            DeviceId = reaction.DeviceId,
            ProfileId = reaction.ProfileId,
            ReactantA = reaction.ReactantA,
            ReactantB = reaction.ReactantB,
            ProductFormula = reaction.ProductFormula,
            ProductDisplayName = reaction.ProductDisplayName,
            ReactionEquation = reaction.ReactionEquation,
            DeltaEnthalpyKjMol = reaction.DeltaEnthalpyKjMol,
            ElectronsTransferred = reaction.ElectronsTransferred,
            IsValid = reaction.IsValid,
            OutcomeType = reaction.OutcomeType,
            ThermodynamicClassification = reaction.ThermodynamicClassification,
            TutorExplanation = reaction.TutorExplanation,
            ExplanationSource = reaction.ExplanationSource,
            Timestamp = reaction.Timestamp,
            UnityOutcomeType = reaction.UnityOutcomeType
        };
    }

    public static ReactionLog ToDomain(ReactionLogDocument document)
    {
        return ReactionLog.Rehydrate(
            document.Id,
            document.EventId,
            document.DeviceId,
            document.ProfileId,
            document.ReactantA,
            document.ReactantB,
            document.ProductFormula,
            document.ReactionEquation,
            document.DeltaEnthalpyKjMol,
            document.ElectronsTransferred,
            document.IsValid,
            document.OutcomeType,
            document.ThermodynamicClassification,
            document.TutorExplanation,
            document.ExplanationSource,
            document.Timestamp,
            document.ProductDisplayName,
            document.UnityOutcomeType);
    }
}
