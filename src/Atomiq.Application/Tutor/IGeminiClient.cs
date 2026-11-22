namespace Atomiq.Application.Tutor;

public interface IGeminiClient
{
    Task<GeminiClientResult> GenerateExplanationAsync(
        ReactionExplanationInput input,
        string systemInstruction,
        CancellationToken cancellationToken = default);
}

public sealed record GeminiClientResult(bool Succeeded, string? Text)
{
    public static GeminiClientResult Success(string text) => new(true, text);

    public static GeminiClientResult Unavailable() => new(false, null);
}
