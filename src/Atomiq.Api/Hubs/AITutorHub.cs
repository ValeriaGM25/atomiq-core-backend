using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Atomiq.Api.Contracts;
using Atomiq.Application.Tutor;
using Atomiq.Application.Validation;
using Atomiq.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Atomiq.Api.Hubs;

[Authorize]
public sealed partial class AITutorHub(
    ExplainReactionUseCase useCase,
    ILogger<AITutorHub> logger) : Hub
{
    private const int MaxReactionJsonBytes = 64 * 1024;
    private const int MaxClientPromptLength = 2048;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ExplainReaction(string reactionJson, string clientSystemPrompt)
    {
        if (string.IsNullOrWhiteSpace(reactionJson)
            || Encoding.UTF8.GetByteCount(reactionJson) > MaxReactionJsonBytes
            || clientSystemPrompt is null
            || clientSystemPrompt.Length > MaxClientPromptLength)
        {
            throw new HubException("The reaction message is invalid or too large.");
        }

        ExplainReactionRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ExplainReactionRequest>(reactionJson, JsonOptions)
                ?? throw new JsonException("Empty reaction payload.");
        }
        catch (JsonException exception)
        {
            LogInvalidPayload(logger, exception);
            throw new HubException("The reaction payload is invalid.");
        }

        try
        {
            var input = MapRequest(request);
            var deviceId = Context.User?.FindFirstValue(StandTokenAuthenticationHandler.DeviceIdClaim);
            if (!InputValidation.IsValidDeviceId(deviceId))
            {
                throw new HubException("The authenticated device is invalid.");
            }

            if (request.EventId is not null && !InputValidation.IsValidEventId(request.EventId))
            {
                throw new HubException("The eventId is invalid.");
            }

            var result = await useCase.ExecuteAsync(
                deviceId!,
                input,
                request.EventId,
                Context.ConnectionAborted);
            var responseJson = JsonSerializer.Serialize(
                new ExplainReactionResponse(result.Explanation, result.Source, result.HistoryDuplicate),
                JsonOptions);
            await Clients.Caller.SendAsync(
                "ReceiveTutorExplanation",
                responseJson,
                Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            LogDisconnected(logger, Context.ConnectionId);
        }
        catch (HubException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogInvocationFailure(logger, exception, Context.ConnectionId);
            throw new HubException("The tutor request could not be completed.");
        }
    }

    private static ReactionExplanationInput MapRequest(ExplainReactionRequest request)
    {
        if (!UnityReactionOutcomeMapper.TryMap(
                request.OutcomeType,
                out var unityOutcomeType,
                out var domainOutcomeType)
            || string.IsNullOrWhiteSpace(request.ReactantA)
            || string.IsNullOrWhiteSpace(request.ReactantB)
            || string.IsNullOrWhiteSpace(request.ProductFormula)
            || string.IsNullOrWhiteSpace(request.ProductDisplayName)
            || string.IsNullOrWhiteSpace(request.ReactionEquation)
            || string.IsNullOrWhiteSpace(request.ThermodynamicClassification)
            || request.ElectronsTransferred is < 0 or > 10000
            || (request.DeltaEnthalpyKjMol is double enthalpy
                && (double.IsNaN(enthalpy) || double.IsInfinity(enthalpy))))
        {
            throw new HubException("The reaction payload is invalid.");
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(request.TimestampUnixMs);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new HubException("The reaction timestamp is invalid.");
        }

        if (timestamp > DateTimeOffset.UtcNow.AddDays(1))
        {
            throw new HubException("The reaction timestamp is invalid.");
        }

        return new ReactionExplanationInput(
            request.ReactantA.Trim(),
            request.ReactantB.Trim(),
            request.ProductFormula.Trim(),
            request.ProductDisplayName.Trim(),
            request.ReactionEquation.Trim(),
            request.IsValid,
            domainOutcomeType,
            request.DeltaEnthalpyKjMol,
            request.ElectronsTransferred,
            request.ThermodynamicClassification.Trim(),
            timestamp.ToUniversalTime(),
            unityOutcomeType);
    }

    [LoggerMessage(EventId = 20, Level = LogLevel.Warning, Message = "Invalid SignalR reaction payload.")]
    private static partial void LogInvalidPayload(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 21, Level = LogLevel.Information, Message = "SignalR connection {ConnectionId} disconnected during tutor request.")]
    private static partial void LogDisconnected(ILogger logger, string connectionId);

    [LoggerMessage(EventId = 22, Level = LogLevel.Error, Message = "SignalR tutor invocation failed for connection {ConnectionId}.")]
    private static partial void LogInvocationFailure(ILogger logger, Exception exception, string connectionId);
}
