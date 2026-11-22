using System.Security.Claims;
using System.Threading.RateLimiting;
using Atomiq.Api.Contracts;
using Atomiq.Application.Validation;
using Atomiq.Application.Profiles;
using Atomiq.Application.Reactions;
using Atomiq.Application.Tutor;
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
builder.Services.AddScoped<ExplainReactionUseCase>();

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

protectedApi.MapPost("/reactions/explain", async (
    ExplainReactionRequest? request,
    ClaimsPrincipal user,
    ExplainReactionUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateExplanationRequest(request, out var input);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    var deviceId = user.FindFirstValue(StandTokenAuthenticationHandler.DeviceIdClaim);
    if (!InputValidation.IsValidDeviceId(deviceId))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "The authenticated device is not valid.");
    }

    var result = await useCase.ExecuteAsync(deviceId!, input!, cancellationToken);
    return Results.Ok(new ExplainReactionResponse(
        result.Explanation,
        result.Source,
        result.HistoryDuplicate));
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

static Dictionary<string, string[]> ValidateExplanationRequest(
    ExplainReactionRequest? request,
    out ReactionExplanationInput? input)
{
    input = null;
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    if (request is null)
    {
        errors["request"] = ["A request body is required."];
        return errors;
    }

    AddRequiredTextError(errors, "reactantA", request.ReactantA, 256);
    AddRequiredTextError(errors, "reactantB", request.ReactantB, 256);
    AddRequiredTextError(errors, "productFormula", request.ProductFormula, 256);
    AddRequiredTextError(errors, "productDisplayName", request.ProductDisplayName, 256);
    AddRequiredTextError(errors, "reactionEquation", request.ReactionEquation, 512);
    AddRequiredTextError(errors, "thermodynamicClassification", request.ThermodynamicClassification, 256);

    if (!Enum.IsDefined((Atomiq.Domain.ReactionOutcomeType)request.OutcomeType))
    {
        errors["outcomeType"] = ["outcomeType is not supported."];
    }

    if (request.DeltaEnthalpyKjMol is double enthalpy
        && (double.IsNaN(enthalpy) || double.IsInfinity(enthalpy)))
    {
        errors["deltaEnthalpyKjMol"] = ["deltaEnthalpyKjMol must be finite."];
    }

    if (request.ElectronsTransferred is < 0 or > 10000)
    {
        errors["electronsTransferred"] = ["electronsTransferred is outside the supported range."];
    }

    try
    {
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(request.TimestampUnixMs);
        if (timestamp > DateTimeOffset.UtcNow.AddDays(1))
        {
            errors["timestampUnixMs"] = ["timestampUnixMs cannot be in the future."];
        }

        if (errors.Count == 0)
        {
            input = new ReactionExplanationInput(
                request.ReactantA!.Trim(),
                request.ReactantB!.Trim(),
                request.ProductFormula!.Trim(),
                request.ProductDisplayName!.Trim(),
                request.ReactionEquation!.Trim(),
                request.IsValid,
                (Atomiq.Domain.ReactionOutcomeType)request.OutcomeType,
                request.DeltaEnthalpyKjMol,
                request.ElectronsTransferred,
                request.ThermodynamicClassification!.Trim(),
                timestamp.ToUniversalTime());
        }
    }
    catch (ArgumentOutOfRangeException)
    {
        errors["timestampUnixMs"] = ["timestampUnixMs is invalid."];
    }

    return errors;
}

static void AddRequiredTextError(
    IDictionary<string, string[]> errors,
    string name,
    string? value,
    int maxLength)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
    {
        errors[name] = [$"{name} is required and must be at most {maxLength} characters."];
    }
}

public partial class Program;
