using System.Security.Claims;
using System.Threading.RateLimiting;
using Atomiq.Api.Contracts;
using Atomiq.Application.Validation;
using Atomiq.Application.Profiles;
using Atomiq.Application.Reactions;
using Atomiq.Infrastructure;
using Atomiq.Infrastructure.Security;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AtomiqExceptionHandler>();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddSignalR();
builder.Services.AddAtomiqInfrastructure(builder.Configuration);
builder.Services.AddScoped<GetOrCreateProfileUseCase>();
builder.Services.AddScoped<GetProfileUseCase>();
builder.Services.AddScoped<AddXpUseCase>();
builder.Services.AddScoped<GetDiscoveredReactionsUseCase>();
builder.Services.AddScoped<SaveReactionLogUseCase>();
builder.Services.AddScoped<GetReactionHistoryUseCase>();

var permitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 60);
var windowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);
if (permitLimit < 1 || windowSeconds < 1)
{
    throw new InvalidOperationException("Rate limiting configuration must use positive values.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (!context.HttpContext.Response.HasStarted)
        {
            await Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Rate limit exceeded.").ExecuteAsync(context.HttpContext);
        }
    };
    options.AddPolicy("mobile-api", httpContext =>
    {
        var deviceId = httpContext.User.FindFirstValue(StandTokenAuthenticationHandler.DeviceIdClaim);
        var partitionKey = deviceId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseCors("ConfiguredOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    timestampUtc = DateTimeOffset.UtcNow
}));

var protectedApi = app.MapGroup("/api")
    .RequireAuthorization()
    .RequireRateLimiting("mobile-api");

protectedApi.MapGet("/profile/{deviceId}", async (
    string deviceId,
    ClaimsPrincipal user,
    GetOrCreateProfileUseCase useCase,
    CancellationToken cancellationToken) =>
{
    if (!InputValidation.IsValidDeviceId(deviceId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["deviceId"] = ["The deviceId format or length is invalid."]
        });
    }

    if (!IsAuthorizedDevice(user, deviceId))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The authenticated device cannot access this resource.");
    }

    var profile = await useCase.ExecuteAsync(deviceId, cancellationToken: cancellationToken);
    return Results.Ok(ToProfileResponse(profile));
});

protectedApi.MapPost("/profile/xp", async (
    AddXpRequest? request,
    ClaimsPrincipal user,
    AddXpUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateXpRequest(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    if (!IsAuthorizedDevice(user, request!.DeviceId!))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The authenticated device cannot access this resource.");
    }

    var result = await useCase.ExecuteAsync(
        request.DeviceId!,
        request.EventId!,
        request.XpGained,
        cancellationToken);
    return result is null
        ? Results.NotFound()
        : Results.Ok(new AddXpResponse(
            result.Profile.DeviceId,
            result.Profile.CurrentLevel,
            result.Profile.TotalXp,
            result.Profile.DiscoveredReactions,
            result.Applied,
            result.DuplicateEvent));
});

protectedApi.MapGet("/reactions/history", async (
    ClaimsPrincipal user,
    GetReactionHistoryUseCase useCase,
    int? page,
    int? pageSize,
    CancellationToken cancellationToken) =>
{
    var deviceId = user.FindFirstValue(StandTokenAuthenticationHandler.DeviceIdClaim);
    if (!InputValidation.IsValidDeviceId(deviceId))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The authenticated device is not valid.");
    }

    var requestedPage = page ?? 1;
    var requestedPageSize = pageSize ?? 20;
    if (requestedPage < 1 || requestedPageSize is < 1 or > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["page"] = ["page must be at least 1."],
            ["pageSize"] = ["pageSize must be between 1 and 100."]
        });
    }

    var result = await useCase.ExecutePageAsync(
        deviceId!,
        requestedPage,
        requestedPageSize,
        cancellationToken);
    return Results.Ok(new ReactionHistoryResponse(
        result.Items,
        result.Page,
        result.PageSize,
        result.HasNextPage));
});

app.Run();

static bool IsAuthorizedDevice(ClaimsPrincipal user, string deviceId)
{
    return string.Equals(
        user.FindFirstValue(StandTokenAuthenticationHandler.DeviceIdClaim),
        deviceId,
        StringComparison.Ordinal);
}

static Dictionary<string, string[]> ValidateXpRequest(AddXpRequest? request)
{
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    if (request is null)
    {
        errors["request"] = ["A request body is required."];
        return errors;
    }

    if (!InputValidation.IsValidDeviceId(request.DeviceId))
    {
        errors["deviceId"] = ["The deviceId format or length is invalid."];
    }

    if (!InputValidation.IsValidEventId(request.EventId))
    {
        errors["eventId"] = ["The eventId format or length is invalid."];
    }

    if (request.XpGained is < 0 or > 1000)
    {
        errors["xpGained"] = ["xpGained must be between 0 and 1000."];
    }

    return errors;
}

static ProfileResponse ToProfileResponse(Atomiq.Application.Profiles.ProfileDto profile)
{
    return new ProfileResponse(
        profile.DeviceId,
        profile.CurrentLevel,
        profile.TotalXp,
        profile.DiscoveredReactions);
}

public partial class Program;
