namespace Atomiq.Infrastructure.Configuration;

public sealed class GeminiSettings
{
    public const string SectionName = "Gemini";

    public string Model { get; init; } = "gemini-placeholder";
    public string ApiKey { get; init; } = "";
}
