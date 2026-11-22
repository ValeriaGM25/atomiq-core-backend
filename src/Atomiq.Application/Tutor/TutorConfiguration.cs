namespace Atomiq.Application.Tutor;

public sealed class TutorConfiguration
{
    public bool Enabled { get; init; }
    public string Model { get; init; } = "";
    public int MaxOutputWords { get; init; } = 25;
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromHours(24);
    public int CacheSizeLimit { get; init; } = 1024;
    public string Language { get; init; } = "es";
    public string PromptVersion { get; init; } = "v1";
}
