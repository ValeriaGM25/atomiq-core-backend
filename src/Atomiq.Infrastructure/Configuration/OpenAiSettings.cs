namespace Atomiq.Infrastructure.Configuration;

public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAI";

    public string BaseUrl { get; init; } = "https://api.openai.com/";
    public string Model { get; init; } = "gpt-4o";
    public string ApiKey { get; init; } = "";
    public int TimeoutMilliseconds { get; init; } = 2500;
    public int MaxOutputWords { get; init; } = 25;
    public bool Enabled { get; init; }
    public double CacheTtlHours { get; init; } = 24;
    public int CacheSizeLimit { get; init; } = 1024;
    public string Language { get; init; } = "es";
    public string PromptVersion { get; init; } = "v1";
}
