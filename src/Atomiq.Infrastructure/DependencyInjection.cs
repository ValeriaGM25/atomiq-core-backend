using Atomiq.Application.Abstractions;
using Atomiq.Infrastructure.Configuration;
using Atomiq.Infrastructure.Mongo;
using Atomiq.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));
        services.AddOptions<StandTokenOptions>()
            .Bind(configuration.GetSection(StandTokenOptions.SectionName));

        services.AddAuthentication(StandTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, StandTokenAuthenticationHandler>(
                StandTokenAuthenticationHandler.SchemeName,
                _ => { });
        services.AddAuthorization();

        services.AddSingleton<MongoDbContext>();
        services.AddSingleton<MongoIndexInitializer>();
        services.AddSingleton<IUserRepository, MongoUserRepository>();
        services.AddSingleton<IReactionHistoryRepository, MongoReactionHistoryRepository>();

        return services;
    }
}
