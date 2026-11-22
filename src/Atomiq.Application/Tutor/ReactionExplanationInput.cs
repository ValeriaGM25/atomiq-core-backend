using Atomiq.Domain;

namespace Atomiq.Application.Tutor;

public sealed record ReactionExplanationInput(
    string ReactantA,
    string ReactantB,
    string ProductFormula,
    string ProductDisplayName,
    string ReactionEquation,
    bool IsValid,
    ReactionOutcomeType OutcomeType,
    double? DeltaEnthalpyKjMol,
    int ElectronsTransferred,
    string ThermodynamicClassification,
    DateTimeOffset Timestamp);

public sealed record TutorExplanation(string Explanation, string Source);

public sealed record ReactionExplanationResult(
    string Explanation,
    string Source,
    bool HistoryDuplicate);
