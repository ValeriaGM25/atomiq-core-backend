using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atomiq.Application.Tutor;
using Atomiq.Domain;
using Atomiq.Infrastructure.Configuration;
using Atomiq.Infrastructure.Gemini;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Atomiq.Application.Tests;

public sealed class TutorTests
{
    [Fact]
    public async Task GeminiExplanationIsCachedAndLimitedToTwentyFiveWords()
    {
        var client = new FakeGeminiClient("Una explicación pedagógica breve con datos proporcionados por Unity.");
        var cache = new FakeExplanationCache();
        var service = new AiTutorService(client, cache, CreateConfiguration(enabled: true));
        var input = CreateInput();

        var first = await service.ExplainReactionAsync(input);
        var second = await service.ExplainReactionAsync(input);

        Assert.Equal("GeminiOnline", first.Source);
        Assert.Equal("MemoryCache", second.Source);
        Assert.Equal(1, client.CallCount);
        Assert.InRange(first.Explanation.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, 1, 25);
    }

    [Fact]
    public async Task DifferentScientificResultsUseDifferentCacheKeys()
    {
        var client = new FakeGeminiClient("Explicación breve.");
        var cache = new FakeExplanationCache();
        var service = new AiTutorService(client, cache, CreateConfiguration(enabled: true));

        await service.ExplainReactionAsync(CreateInput());
        await service.ExplainReactionAsync(CreateInput() with { IsValid = false });

        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task MissingProviderFallsBackWithoutClaimingGemini()
    {
        var client = new FakeGeminiClient(null);
        var service = new AiTutorService(client, new FakeExplanationCache(), CreateConfiguration(enabled: true));

        var result = await service.ExplainReactionAsync(CreateInput());

        Assert.Equal("OfflineFallback", result.Source);
        Assert.DoesNotContain("Gemini", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderTextOverTwentyFiveWordsUsesValidatedFallback()
    {
        var tooLong = string.Join(' ', Enumerable.Repeat("palabra", 26));
        var service = new AiTutorService(
            new FakeGeminiClient(tooLong),
            new FakeExplanationCache(),
            CreateConfiguration(enabled: true));

        var result = await service.ExplainReactionAsync(CreateInput());

        Assert.Equal("OfflineFallback", result.Source);
    }

    [Fact]
    public async Task DisabledProviderUsesFallback()
    {
        var client = new FakeGeminiClient("Nunca debe usarse.");
        var service = new AiTutorService(client, new FakeExplanationCache(), CreateConfiguration(enabled: false));

        var result = await service.ExplainReactionAsync(CreateInput());

        Assert.Equal("OfflineFallback", result.Source);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GeminiClientParsesValidResponseThroughMockedHandler()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = "Explicación remota breve." } } } }
                }
            })
        });
        var settings = Options.Create(new GeminiSettings
        {
            Enabled = true,
            BaseUrl = "https://example.invalid",
            Model = "test-model",
            ApiKey = "test-only-key",
            TimeoutMilliseconds = 1000,
            MaxOutputWords = 25
        });
        var client = new GeminiClient(new HttpClient(handler), settings, NullLogger<GeminiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "backend system instruction");

        Assert.True(result.Succeeded);
        Assert.Equal("Explicación remota breve.", result.Text);
        Assert.Equal("x-goog-api-key", handler.Request!.Headers.First(header =>
            header.Key.Equals("x-goog-api-key", StringComparison.OrdinalIgnoreCase)).Key);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GeminiTransientHttpErrorsUseUnavailableResult(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
        var client = new GeminiClient(
            new HttpClient(handler),
            Options.Create(new GeminiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "test-model",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<GeminiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GeminiMalformedJsonUsesUnavailableResult()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ malformed")
        });
        var client = new GeminiClient(
            new HttpClient(handler),
            Options.Create(new GeminiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "test-model",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<GeminiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CancellationIsForwardedToTutorClient()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var service = new AiTutorService(
            new FakeGeminiClient("unused"),
            new FakeExplanationCache(),
            CreateConfiguration(enabled: true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExplainReactionAsync(CreateInput(), source.Token));
    }

    private static TutorConfiguration CreateConfiguration(bool enabled) => new()
    {
        Enabled = enabled,
        Model = "test-model",
        MaxOutputWords = 25,
        CacheTtl = TimeSpan.FromHours(24),
        Language = "es",
        PromptVersion = "test-v1"
    };

    private static ReactionExplanationInput CreateInput() => new(
        "Na",
        "Cl",
        "NaCl",
        "Cloruro de Sodio (Red Cristalina)",
        "Na+ + Cl- -> NaCl (s)",
        true,
        ReactionOutcomeType.Valid,
        -411.2,
        1,
        "Exotérmica",
        DateTimeOffset.FromUnixTimeMilliseconds(1732068000000));

    private sealed class FakeGeminiClient(string? text) : IGeminiClient
    {
        public int CallCount { get; private set; }

        public Task<GeminiClientResult> GenerateExplanationAsync(
            ReactionExplanationInput input,
            string systemInstruction,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(text is null
                ? GeminiClientResult.Unavailable()
                : GeminiClientResult.Success(text));
        }
    }

    private sealed class FakeExplanationCache : IReactionExplanationCache
    {
        private readonly Dictionary<string, TutorExplanation> entries = new(StringComparer.Ordinal);

        public TutorExplanation? GetCached(string key)
        {
            return entries.GetValueOrDefault(key);
        }

        public void SetCached(string key, TutorExplanation explanation, TimeSpan ttl)
        {
            entries[key] = explanation;
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
