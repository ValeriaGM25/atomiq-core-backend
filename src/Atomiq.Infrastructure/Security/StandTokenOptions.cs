namespace Atomiq.Infrastructure.Security;

public sealed class StandTokenOptions
{
    public const string SectionName = "Authentication";

    public Dictionary<string, string> DeviceTokens { get; init; } = new(StringComparer.Ordinal);
}
