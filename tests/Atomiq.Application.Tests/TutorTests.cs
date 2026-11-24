using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atomiq.Application.Tutor;
using Atomiq.Domain;
using Atomiq.Infrastructure.Configuration;
using Atomiq.Infrastructure.OpenAi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Atomiq.Application.Tests;

public sealed class TutorTests
{
    [Fact]
    public async Task OpenAiExplanationIsCachedAndLimitedToTwentyFiveWords()
    {
        var client = new FakeOpenAiClient("Una explicación pedagógica breve con datos proporcionados por Unity.");
        var cache = new FakeExplanationCache();
        var service = new AiTutorService(client, cache, CreateConfiguration(enabled: true));
        var input = CreateInput();

        var first = await service.ExplainReactionAsync(input);
        var second = await service.ExplainReactionAsync(input);

        Assert.Equal("OpenAIOnline", first.Source);
        Assert.Equal("MemoryCache", second.Source);
        Assert.Equal(1, client.CallCount);
        Assert.InRange(first.Explanation.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length, 1, 25);
    }

    [Fact]
    public async Task DifferentScientificResultsUseDifferentCacheKeys()
    {
        var client = new FakeOpenAiClient("Explicación breve.");
        var cache = new FakeExplanationCache();
        var service = new AiTutorService(client, cache, CreateConfiguration(enabled: true));

        await service.ExplainReactionAsync(CreateInput());
        await service.ExplainReactionAsync(CreateInput() with { IsValid = false });

        Assert.Equal(2, client.CallCount);
    }

    [Fact]
    public async Task SameExplanationAtAnotherExecutionTimeHitsCache()
    {
        var client = new FakeOpenAiClient("Explicación breve.");
        var cache = new FakeExplanationCache();
        var service = new AiTutorService(client, cache, CreateConfiguration(enabled: true));

        await service.ExplainReactionAsync(CreateInput());
        await service.ExplainReactionAsync(CreateInput() with
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1732068001000)
        });

        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task MissingProviderFallsBackWithoutClaimingOpenAi()
    {
        var client = new FakeOpenAiClient(null);
        var service = new AiTutorService(client, new FakeExplanationCache(), CreateConfiguration(enabled: true));

        var result = await service.ExplainReactionAsync(CreateInput());

        Assert.Equal("OfflineFallback", result.Source);
        Assert.DoesNotContain("OpenAI", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderTextOverTwentyFiveWordsUsesValidatedFallback()
    {
        var tooLong = string.Join(' ', Enumerable.Repeat("palabra", 26));
        var service = new AiTutorService(
            new FakeOpenAiClient(tooLong),
            new FakeExplanationCache(),
            CreateConfiguration(enabled: true));

        var result = await service.ExplainReactionAsync(CreateInput());

        Assert.Equal("OfflineFallback", result.Source);
    }

    [Fact]
    public async Task DisabledProviderUsesFallback()
    {
        var client = new FakeOpenAiClient("Nunca debe usarse.");
        var service = new AiTutorService(client, new FakeExplanationCache(), CreateConfiguration(enabled: false));

        var result = await service.ExplainReactionAsync(CreateInput());

        Assert.Equal("OfflineFallback", result.Source);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task OpenAiClientParsesValidResponsesApiThroughMockedHandler()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                output = new[]
                {
                    new { content = new[] { new { type = "output_text", text = "Explicación remota breve." } } }
                }
            })
        });
        var settings = Options.Create(new OpenAiSettings
        {
            Enabled = true,
            BaseUrl = "https://example.invalid",
            Model = "test-model",
            ApiKey = "test-only-key",
            TimeoutMilliseconds = 1000,
            MaxOutputWords = 25
        });
        var client = new OpenAiClient(new HttpClient(handler), settings, NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "backend system instruction");

        Assert.True(result.Succeeded);
        Assert.Equal("Explicación remota breve.", result.Text);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.EndsWith("/v1/responses", handler.Request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("test-only-key", handler.Request.Headers.Authorization.Parameter);
        using var requestDocument = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("test-model", requestDocument.RootElement.GetProperty("model").GetString());
        Assert.Equal("backend system instruction", requestDocument.RootElement.GetProperty("instructions").GetString());
        Assert.Contains("Na", requestDocument.RootElement.GetProperty("input").GetString()!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OpenAiTransientHttpErrorsUseUnavailableResult(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
        var client = new OpenAiClient(
            new HttpClient(handler),
            Options.Create(new OpenAiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "test-model",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task OpenAiMalformedJsonUsesUnavailableResult()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ malformed")
        });
        var client = new OpenAiClient(
            new HttpClient(handler),
            Options.Create(new OpenAiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "test-model",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task OpenAiAuthenticationErrorsUseUnavailableResult(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
        var client = new OpenAiClient(
            new HttpClient(handler),
            Options.Create(new OpenAiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "gpt-4o",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task OpenAiUnexpectedResponseUsesUnavailableResult()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { output = Array.Empty<object>() })
        });
        var client = new OpenAiClient(
            new HttpClient(handler),
            Options.Create(new OpenAiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "gpt-4o",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task OpenAiTimeoutUsesUnavailableResult()
    {
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("simulated timeout"));
        var client = new OpenAiClient(
            new HttpClient(handler),
            Options.Create(new OpenAiSettings
            {
                Enabled = true,
                BaseUrl = "https://example.invalid",
                Model = "gpt-4o",
                ApiKey = "test-only-key",
                TimeoutMilliseconds = 1000
            }),
            NullLogger<OpenAiClient>.Instance);

        var result = await client.GenerateExplanationAsync(CreateInput(), "system");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CancellationIsForwardedToTutorClient()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var service = new AiTutorService(
            new FakeOpenAiClient("unused"),
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

    private sealed class FakeOpenAiClient(string? text) : IOpenAiClient
    {
        public int CallCount { get; private set; }

        public Task<OpenAiClientResult> GenerateExplanationAsync(
            ReactionExplanationInput input,
            string systemInstruction,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(text is null
                ? OpenAiClientResult.Unavailable()
                : OpenAiClientResult.Success(text));
        }

        public Task<OpenAiClientResult> GenerateTextAsync(
            string systemInstruction,
            string userInput,
            int maxOutputTokens,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The explanation tests do not use free-text generation.");
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
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
