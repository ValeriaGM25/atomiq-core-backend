using Atomiq.Application.Abstractions;
using Atomiq.Application.Tutor;
using Atomiq.Infrastructure.Configuration;
using Atomiq.Infrastructure.Gemini;
using Atomiq.Infrastructure.Mongo;
using Atomiq.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace Atomiq.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAtomiqInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MongoSettings>()
            .Bind(configuration.GetSection(MongoSettings.SectionName))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.DatabaseName),
                "Mongo:DatabaseName must be configured.")
            .ValidateOnStart();
        services.AddOptions<GeminiSettings>()
            .Bind(configuration.GetSection(GeminiSettings.SectionName))
            .Validate(settings => settings.TimeoutMilliseconds is >= 250 and <= 10000,
                "Gemini:TimeoutMilliseconds must be between 250 and 10000.")
            .Validate(settings => settings.MaxOutputWords is >= 1 and <= 25,
                "Gemini:MaxOutputWords must be between 1 and 25.")
            .Validate(settings => settings.CacheTtlHours is > 0 and <= 168,
                "Gemini:CacheTtlHours must be greater than zero and no more than 168.")
            .Validate(settings => settings.CacheSizeLimit is >= 1 and <= 10000,
                "Gemini:CacheSizeLimit must be between 1 and 10000.")
            .Validate(settings => !settings.Enabled ||
                (!string.IsNullOrWhiteSpace(settings.BaseUrl) &&
                 !string.IsNullOrWhiteSpace(settings.Model) &&
                 !string.IsNullOrWhiteSpace(settings.ApiKey)),
                "Enabled Gemini requires BaseUrl, Model and ApiKey.")
            .ValidateOnStart();
        services.AddOptions<StandTokenOptions>()
            .Bind(configuration.GetSection(StandTokenOptions.SectionName));

        services.AddAuthentication(StandTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, StandTokenAuthenticationHandler>(
                StandTokenAuthenticationHandler.SchemeName,
                _ => { });
        services.AddAuthorization();

        services.AddMemoryCache(options =>
            options.SizeLimit = configuration.GetValue("Gemini:CacheSizeLimit", 1024));
        services.AddSingleton<TutorConfiguration>(provider =>
        {
            var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiSettings>>().Value;
            return new TutorConfiguration
            {
                Enabled = settings.Enabled,
                Model = settings.Model,
                MaxOutputWords = settings.MaxOutputWords,
                CacheTtl = TimeSpan.FromHours(settings.CacheTtlHours),
                CacheSizeLimit = settings.CacheSizeLimit,
                Language = settings.Language,
                PromptVersion = settings.PromptVersion
            };
        });
        services.AddSingleton<IReactionExplanationCache, MemoryReactionExplanationCache>();
        services.AddHttpClient<IGeminiClient, GeminiClient>((provider, client) =>
        {
            var settings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiSettings>>().Value;
            client.Timeout = TimeSpan.FromMilliseconds(settings.TimeoutMilliseconds);
        });
        services.AddScoped<IAiTutorService, AiTutorService>();
        services.AddScoped<ExplainReactionUseCase>();

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton<MongoIndexInitializer>();
        services.AddSingleton<IUserRepository, MongoUserRepository>();
        services.AddSingleton<IReactionHistoryRepository, MongoReactionHistoryRepository>();

        return services;
    }
}
