namespace Atomiq.Domain;

public sealed class ReactionLog
{
    private ReactionLog(
        string id,
        string eventId,
        string deviceId,
        string profileId,
        string reactantA,
        string reactantB,
        string productFormula,
        string reactionEquation,
        double? deltaEnthalpyKjMol,
        int electronsTransferred,
        bool isValid,
        ReactionOutcomeType outcomeType,
        string thermodynamicClassification,
        string? tutorExplanation,
        string? explanationSource,
        DateTimeOffset timestamp)
    {
        Id = RequireIdentifier(id, nameof(id));
        EventId = RequireIdentifier(eventId, nameof(eventId));
        DeviceId = RequireIdentifier(deviceId, nameof(deviceId));
        ProfileId = RequireIdentifier(profileId, nameof(profileId));
        ReactantA = FormulaNormalizer.NormalizeRequired(reactantA, nameof(reactantA));
        ReactantB = FormulaNormalizer.NormalizeRequired(reactantB, nameof(reactantB));
        ProductFormula = FormulaNormalizer.NormalizeRequired(productFormula, nameof(productFormula));
        ReactionEquation = FormulaNormalizer.NormalizeRequired(reactionEquation, nameof(reactionEquation));
        if (double.IsNaN(deltaEnthalpyKjMol ?? 0) || double.IsInfinity(deltaEnthalpyKjMol ?? 0))
        {
            throw new DomainException("Delta enthalpy must be finite when provided.");
        }

        if (electronsTransferred < 0)
        {
            throw new DomainException("Electrons transferred cannot be negative.");
        }

        DeltaEnthalpyKjMol = deltaEnthalpyKjMol;
        ElectronsTransferred = electronsTransferred;
        IsValid = isValid;
        OutcomeType = outcomeType;
        ThermodynamicClassification = FormulaNormalizer.NormalizeRequired(
            thermodynamicClassification,
            nameof(thermodynamicClassification));
        TutorExplanation = NormalizeOptional(tutorExplanation);
        ExplanationSource = NormalizeOptional(explanationSource);
        Timestamp = timestamp.ToUniversalTime();
    }

    public string Id { get; }
    public string EventId { get; }
    public string DeviceId { get; }
    public string ProfileId { get; }
    public string ReactantA { get; }
    public string ReactantB { get; }
    public string ProductFormula { get; }
    public string ReactionEquation { get; }
    public double? DeltaEnthalpyKjMol { get; }
    public int ElectronsTransferred { get; }
    public bool IsValid { get; }
    public ReactionOutcomeType OutcomeType { get; }
    public string ThermodynamicClassification { get; }
    public string? TutorExplanation { get; }
    public string? ExplanationSource { get; }
    public DateTimeOffset Timestamp { get; }

    public static ReactionLog Create(
        string eventId,
        string deviceId,
        string profileId,
        string reactantA,
        string reactantB,
        string productFormula,
        string reactionEquation,
        double? deltaEnthalpyKjMol,
        int electronsTransferred,
        bool isValid,
        ReactionOutcomeType outcomeType,
        string thermodynamicClassification,
        string? tutorExplanation,
        string? explanationSource,
        DateTimeOffset? timestamp = null,
        string? id = null)
    {
        return new ReactionLog(
            id ?? Guid.NewGuid().ToString("N"),
            eventId,
            deviceId,
            profileId,
            reactantA,
            reactantB,
            productFormula,
            reactionEquation,
            deltaEnthalpyKjMol,
            electronsTransferred,
            isValid,
            outcomeType,
            thermodynamicClassification,
            tutorExplanation,
            explanationSource,
            timestamp ?? DateTimeOffset.UtcNow);
    }

    public static ReactionLog Rehydrate(
        string id,
        string eventId,
        string deviceId,
        string profileId,
        string reactantA,
        string reactantB,
        string productFormula,
        string reactionEquation,
        double? deltaEnthalpyKjMol,
        int electronsTransferred,
        bool isValid,
        ReactionOutcomeType outcomeType,
        string thermodynamicClassification,
        string? tutorExplanation,
        string? explanationSource,
        DateTimeOffset timestamp)
    {
        return new ReactionLog(
            id,
            eventId,
            deviceId,
            profileId,
            reactantA,
            reactantB,
            productFormula,
            reactionEquation,
            deltaEnthalpyKjMol,
            electronsTransferred,
            isValid,
            outcomeType,
            thermodynamicClassification,
            tutorExplanation,
            explanationSource,
            timestamp);
    }

    private static string RequireIdentifier(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
