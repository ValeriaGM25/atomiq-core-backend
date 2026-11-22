using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atomiq.Infrastructure.Security;

public sealed class StandTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IOptionsMonitor<StandTokenOptions> tokenOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StandToken";
    public const string HeaderName = "X-Atomiq-Device-Token";
    public const string DeviceIdClaim = "device_id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (headerValues.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid device token."));
        }

        var presentedToken = headerValues[0];
        if (string.IsNullOrWhiteSpace(presentedToken) || presentedToken.Length > 256)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid device token."));
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presentedToken);
        foreach (var configuredToken in tokenOptions.CurrentValue.DeviceTokens)
        {
            var configuredBytes = Encoding.UTF8.GetBytes(configuredToken.Value);
            if (CryptographicOperations.FixedTimeEquals(presentedBytes, configuredBytes))
            {
                var claims = new[]
                {
                    new Claim(DeviceIdClaim, configuredToken.Key),
                    new Claim(ClaimTypes.NameIdentifier, configuredToken.Key)
                };
                var identity = new ClaimsIdentity(claims, SchemeName);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(identity),
                    SchemeName)));
            }
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid device token."));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication is required.").ExecuteAsync(Context);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The authenticated device cannot access this resource.").ExecuteAsync(Context);
    }
}
