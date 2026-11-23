using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Atomiq.Infrastructure.Mongo;

[BsonIgnoreExtraElements]
public sealed class UserProfileDocument
{
    [BsonId]
    public string Id { get; set; } = null!;

    [BsonElement("deviceId")]
    public string DeviceId { get; set; } = null!;

    [BsonElement("username")]
    [BsonIgnoreIfNull]
    public string? Username { get; set; }

    [BsonElement("currentLevel")]
    public int CurrentLevel { get; set; }

    [BsonElement("totalXp")]
    public int TotalXp { get; set; }

    [BsonElement("discoveredReactions")]
    public List<string> DiscoveredReactions { get; set; } = [];

    [BsonElement("appliedXpEventIds")]
    public List<string> AppliedXpEventIds { get; set; } = [];

    [BsonElement("createdAt")]
    [BsonSerializer(typeof(DateTimeOffsetSerializer))]
    public DateTimeOffset CreatedAt { get; set; }

    [BsonElement("lastActiveAt")]
    [BsonSerializer(typeof(DateTimeOffsetSerializer))]
    public DateTimeOffset LastActiveAt { get; set; }
}
