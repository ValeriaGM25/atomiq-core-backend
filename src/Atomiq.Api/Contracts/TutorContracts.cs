namespace Atomiq.Api.Contracts;

public sealed record ExplainReactionRequest(
    string? ReactantA,
    string? ReactantB,
    string? ProductFormula,
    string? ProductDisplayName,
    string? ReactionEquation,
    bool IsValid,
    int OutcomeType,
    double? DeltaEnthalpyKjMol,
    int ElectronsTransferred,
    string? ThermodynamicClassification,
    long TimestampUnixMs);

public sealed record ExplainReactionResponse(
    string Explanation,
    string Source,
    bool HistoryDuplicate);
