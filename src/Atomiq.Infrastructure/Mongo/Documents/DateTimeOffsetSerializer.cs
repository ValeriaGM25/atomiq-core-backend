using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Atomiq.Infrastructure.Mongo;

public sealed class DateTimeOffsetSerializer : SerializerBase<DateTimeOffset>
{
    public override DateTimeOffset Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        return context.Reader.GetCurrentBsonType() switch
        {
            BsonType.DateTime => DateTimeOffset.FromUnixTimeMilliseconds(context.Reader.ReadDateTime()),
            BsonType.String => DateTimeOffset.Parse(context.Reader.ReadString(), null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime(),
            _ => throw new FormatException("Expected a BSON date or ISO-8601 string for DateTimeOffset.")
        };
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTimeOffset value)
    {
        context.Writer.WriteDateTime(value.ToUniversalTime().ToUnixTimeMilliseconds());
    }
}
