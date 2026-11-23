using System.Security.Cryptography;
using System.Text;

namespace Atomiq.Application.Tutor;

public sealed class AiTutorService(
    IOpenAiClient openAiClient,
    IReactionExplanationCache cache,
    TutorConfiguration configuration) : IAiTutorService, IDisposable
{
    private const string SystemInstruction = "Eres Atomiq AI, tutor pedagógico de química para estudiantes. Explica resultados de simulaciones educativas en español, con precisión científica, lenguaje accesible y un máximo de 25 palabras. No inventes reacciones, datos termodinámicos ni resultados. No confundas un modelo pedagógico con una simulación cuántica real.";
    private readonly SemaphoreSlim cacheGate = new(1, 1);

    public async Task<TutorExplanation> ExplainReactionAsync(
        ReactionExplanationInput input,
        CancellationToken cancellationToken = default)
    {
        ReactionExplanationValidator.Validate(input);
        var key = CreateCacheKey(input);
        var cached = cache.GetCached(key);
        if (cached is not null)
        {
            return cached with { Source = "MemoryCache" };
        }

        await cacheGate.WaitAsync(cancellationToken);
        try
        {
            cached = cache.GetCached(key);
            if (cached is not null)
            {
                return cached with { Source = "MemoryCache" };
            }

            TutorExplanation explanation;
            if (configuration.Enabled)
            {
                OpenAiClientResult generated;
                try
                {
                    generated = await openAiClient.GenerateExplanationAsync(input, SystemInstruction, cancellationToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    generated = OpenAiClientResult.Unavailable();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    generated = OpenAiClientResult.Unavailable();
                }

                explanation = generated.Succeeded && TrySanitize(generated.Text, out var sanitized)
                    ? new TutorExplanation(sanitized, "OpenAIOnline")
                    : FallbackTutorService.Explain(input);
            }
            else
            {
                explanation = FallbackTutorService.Explain(input);
            }

            cache.SetCached(key, explanation, configuration.CacheTtl);
            return explanation;
        }
        finally
        {
            cacheGate.Release();
        }
    }

    private string CreateCacheKey(ReactionExplanationInput input)
    {
        var material = string.Join("|", new[]
        {
            "OpenAI",
            Normalize(input.ReactantA),
            Normalize(input.ReactantB),
            Normalize(input.ProductFormula),
            Normalize(input.ProductDisplayName),
            Normalize(input.ReactionEquation),
            input.IsValid.ToString(),
            input.OutcomeType.ToString(),
            input.DeltaEnthalpyKjMol?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            input.ElectronsTransferred.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Normalize(input.ThermodynamicClassification),
            input.UnityOutcomeType?.ToString() ?? "null",
            configuration.Language,
            configuration.PromptVersion,
            configuration.Model,
            configuration.Enabled.ToString()
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private bool TrySanitize(string? value, out string sanitized)
    {
        sanitized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.Contains('>'))
        {
            return false;
        }

        var normalized = value.Normalize();
        normalized = normalized.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
        sanitized = string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var wordCount = sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return sanitized.Length > 0 && wordCount <= configuration.MaxOutputWords;
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value.Normalize().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public void Dispose()
    {
        cacheGate.Dispose();
    }
}
