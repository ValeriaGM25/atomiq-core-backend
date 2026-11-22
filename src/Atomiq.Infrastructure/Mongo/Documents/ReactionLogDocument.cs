using Atomiq.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Atomiq.Infrastructure.Mongo;

[BsonIgnoreExtraElements]
public sealed class ReactionLogDocument
{
    [BsonId]
    public string Id { get; set; } = null!;

    [BsonElement("eventId")]
    public string EventId { get; set; } = null!;

    [BsonElement("deviceId")]
    public string DeviceId { get; set; } = null!;

    [BsonElement("profileId")]
    public string ProfileId { get; set; } = null!;

    [BsonElement("reactantA")]
    public string ReactantA { get; set; } = null!;

    [BsonElement("reactantB")]
    public string ReactantB { get; set; } = null!;

    [BsonElement("productFormula")]
    public string ProductFormula { get; set; } = null!;

    [BsonElement("reactionEquation")]
    public string ReactionEquation { get; set; } = null!;

    [BsonElement("deltaEnthalpyKjMol")]
    [BsonIgnoreIfNull]
    public double? DeltaEnthalpyKjMol { get; set; }

    [BsonElement("electronsTransferred")]
    public int ElectronsTransferred { get; set; }

    [BsonElement("isValid")]
    public bool IsValid { get; set; }

    [BsonElement("outcomeType")]
    [BsonRepresentation(BsonType.String)]
    public ReactionOutcomeType OutcomeType { get; set; }

    [BsonElement("thermodynamicClassification")]
    public string ThermodynamicClassification { get; set; } = null!;

    [BsonElement("tutorExplanation")]
    [BsonIgnoreIfNull]
    public string? TutorExplanation { get; set; }

    [BsonElement("explanationSource")]
    [BsonIgnoreIfNull]
    public string? ExplanationSource { get; set; }

    [BsonElement("timestamp")]
    [BsonSerializer(typeof(DateTimeOffsetSerializer))]
    public DateTimeOffset Timestamp { get; set; }
}
