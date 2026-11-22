namespace Atomiq.Infrastructure.Configuration;

public sealed class GeminiSettings
{
    public const string SectionName = "Gemini";

    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/";
    public string Model { get; init; } = "gemini-3.5-flash-lite";
    public string ApiKey { get; init; } = "";
    public int TimeoutMilliseconds { get; init; } = 2500;
    public int MaxOutputWords { get; init; } = 25;
    public bool Enabled { get; init; }
    public double CacheTtlHours { get; init; } = 24;
    public int CacheSizeLimit { get; init; } = 1024;
    public string Language { get; init; } = "es";
    public string PromptVersion { get; init; } = "v1";
}
