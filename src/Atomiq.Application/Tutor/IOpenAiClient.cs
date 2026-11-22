namespace Atomiq.Application.Tutor;

public interface IOpenAiClient
{
    Task<OpenAiClientResult> GenerateExplanationAsync(
        ReactionExplanationInput input,
        string systemInstruction,
        CancellationToken cancellationToken = default);
}

public sealed record OpenAiClientResult(bool Succeeded, string? Text)
{
    public static OpenAiClientResult Success(string text) => new(true, text);

    public static OpenAiClientResult Unavailable() => new(false, null);
}
