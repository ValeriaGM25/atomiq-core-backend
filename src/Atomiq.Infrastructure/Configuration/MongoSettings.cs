namespace Atomiq.Infrastructure.Configuration;

public sealed class MongoSettings
{
    public const string SectionName = "Mongo";

    public string DatabaseName { get; init; } = "atomiq";
    public string ConnectionString { get; init; } = "";
}
