using Atomiq.Domain;
using Atomiq.Infrastructure.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Atomiq.Application.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainAssemblyIsAvailableToApplicationTests()
    {
        Assert.Equal("Atomiq.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void MongoDocumentUsesExplicitCamelCaseFieldsAndStringEnum()
    {
        var document = new ReactionLogDocument
        {
            Id = "reaction-1",
            EventId = "event-1",
            DeviceId = "device-1",
            ProfileId = "profile-1",
            ReactantA = "H2",
            ReactantB = "O2",
            ProductFormula = "H2O",
            ReactionEquation = "2H2+O2->2H2O",
            ElectronsTransferred = 0,
            IsValid = true,
            OutcomeType = ReactionOutcomeType.Valid,
            ThermodynamicClassification = "Exothermic",
            Timestamp = DateTimeOffset.UtcNow,
            UnityOutcomeType = 2
        };

        var bson = document.ToBsonDocument();

        Assert.Equal("reaction-1", bson["_id"].AsString);
        Assert.Equal("event-1", bson["eventId"].AsString);
        Assert.Equal("Valid", bson["outcomeType"].AsString);
        Assert.Equal(2, bson["unityOutcomeType"].AsInt32);
        Assert.True(bson.Contains("timestamp"));

        var roundTrip = BsonSerializer.Deserialize<ReactionLogDocument>(bson);
        Assert.Equal(
            document.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds(),
            roundTrip.Timestamp.ToUnixTimeMilliseconds());
        Assert.Equal(2, roundTrip.UnityOutcomeType);
    }

    [Fact]
    public void LegacyMongoDocumentWithoutUnityMetadataStillDeserializes()
    {
        var legacy = new BsonDocument
        {
            ["_id"] = "legacy-reaction",
            ["eventId"] = "legacy-event",
            ["deviceId"] = "device-1",
            ["profileId"] = "profile-1",
            ["reactantA"] = "Na",
            ["reactantB"] = "Cl",
            ["productFormula"] = "NaCl",
            ["reactionEquation"] = "Na+Cl->NaCl",
            ["electronsTransferred"] = 1,
            ["isValid"] = true,
            ["outcomeType"] = "Valid",
            ["thermodynamicClassification"] = "Exothermic",
            ["timestamp"] = new BsonDateTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        };

        var document = BsonSerializer.Deserialize<ReactionLogDocument>(legacy);

        Assert.Equal("legacy-event", document.EventId);
        Assert.Null(document.UnityOutcomeType);
    }
}
