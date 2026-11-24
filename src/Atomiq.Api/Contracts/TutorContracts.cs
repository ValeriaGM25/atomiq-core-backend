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
    long TimestampUnixMs,
    string? EventId = null);

public sealed record ExplainReactionResponse(
    string Explanation,
    string Source,
    bool HistoryDuplicate);

/// <summary>Previous chat message; <c>Role</c> is "student" or "tutor".</summary>
public sealed record TutorChatTurnRequest(string? Role, string? Text);

public sealed record TutorChatRequest(
    string? Message,
    TutorChatTurnRequest[]? History,
    string? Challenge,
    string? LastReaction);

public sealed record TutorChatResponse(string Reply, string Source);
