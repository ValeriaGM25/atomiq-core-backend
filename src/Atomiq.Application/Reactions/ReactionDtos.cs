using Atomiq.Domain;

namespace Atomiq.Application.Reactions;

public sealed record ReactionLogDto(
    string Id,
    string EventId,
    string DeviceId,
    string ProfileId,
    string ReactantA,
    string ReactantB,
    string ProductFormula,
    string ReactionEquation,
    double? DeltaEnthalpyKjMol,
    int ElectronsTransferred,
    bool IsValid,
    ReactionOutcomeType OutcomeType,
    string ThermodynamicClassification,
    string? TutorExplanation,
    string? ExplanationSource,
    DateTimeOffset Timestamp);

public sealed record SaveReactionResult(ReactionLogDto Reaction, bool Inserted, bool DuplicateEvent);
