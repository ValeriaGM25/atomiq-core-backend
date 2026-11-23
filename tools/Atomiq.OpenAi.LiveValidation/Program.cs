using System.Diagnostics;
using Atomiq.Application.Tutor;
using Atomiq.Domain;
using Atomiq.Infrastructure.Caching;
using Atomiq.Infrastructure.Configuration;
using Atomiq.Infrastructure.OpenAi;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

const string optInValue = "I_UNDERSTAND_ONE_PAID_OPENAI_CALL";
if (!string.Equals(
        Environment.GetEnvironmentVariable("ATOMIQ_ALLOW_OPENAI_LIVE_TEST"),
        optInValue,
        StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        "Live OpenAI validation is disabled. Set ATOMIQ_ALLOW_OPENAI_LIVE_TEST=" +
        optInValue + " only after explicit operator authorization.");
    return 2;
}

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
var settings = configuration.GetSection(OpenAiSettings.SectionName).Get<OpenAiSettings>()
    ?? new OpenAiSettings();

if (!settings.Enabled
    || string.IsNullOrWhiteSpace(settings.ApiKey)
    || string.IsNullOrWhiteSpace(settings.Model)
    || settings.MaxOutputWords is < 1 or > 25
    || settings.TimeoutMilliseconds is < 250 or > 10000)
{
    Console.Error.WriteLine(
        "Live OpenAI validation requires OpenAI:Enabled=true, a User Secret ApiKey, " +
        "a model and valid timeout/output limits.");
    return 3;
}

using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds)
};
var client = new OpenAiClient(
    httpClient,
    Options.Create(settings),
    NullLogger<OpenAiClient>.Instance);
using var memoryCache = new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = settings.CacheSizeLimit
});
using var tutor = new AiTutorService(
    client,
    new MemoryReactionExplanationCache(memoryCache),
    new TutorConfiguration
    {
        Enabled = settings.Enabled,
        Model = settings.Model,
        MaxOutputWords = settings.MaxOutputWords,
        CacheTtl = TimeSpan.FromHours(settings.CacheTtlHours),
        CacheSizeLimit = settings.CacheSizeLimit,
        Language = settings.Language,
        PromptVersion = settings.PromptVersion
    });

var input = new ReactionExplanationInput(
    "Na",
    "Cl",
    "NaCl",
    "Cloruro de sodio",
    "Na + Cl -> NaCl",
    true,
    ReactionOutcomeType.Valid,
    -411.2,
    1,
    "Exotermica",
    DateTimeOffset.UtcNow);

var stopwatch = Stopwatch.StartNew();
var result = await tutor.ExplainReactionAsync(input);
stopwatch.Stop();
var wordCount = result.Explanation.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

Console.WriteLine($"Source: {result.Source}");
Console.WriteLine($"LatencyMs: {stopwatch.Elapsed.TotalMilliseconds:F0}");
Console.WriteLine($"WordCount: {wordCount}");
Console.WriteLine($"Explanation: {result.Explanation}");

if (!string.Equals(result.Source, "OpenAIOnline", StringComparison.Ordinal)
    || wordCount is < 1 or > 25)
{
    Console.Error.WriteLine(
        "Live validation did not receive a valid OpenAIOnline response; fallback is not a live PASS.");
    return 4;
}

Console.WriteLine("OpenAI live validation passed. Exactly one provider request was permitted.");
return 0;
