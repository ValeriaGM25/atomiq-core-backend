using System.Security.Cryptography;
using System.Text;
using Atomiq.Application.Abstractions;
using Atomiq.Application.Profiles;
using Atomiq.Application.Reactions;
using Atomiq.Domain;

namespace Atomiq.Application.Tutor;

public sealed class ExplainReactionUseCase(
    IAiTutorService tutor,
    GetOrCreateProfileUseCase profiles,
    SaveReactionLogUseCase history)
{
    public async Task<ReactionExplanationResult> ExecuteAsync(
        string deviceId,
        ReactionExplanationInput input,
        string? eventId = null,
        CancellationToken cancellationToken = default)
    {
        ReactionExplanationValidator.Validate(input);
        eventId = NormalizeEventId(eventId);
        var profile = await profiles.ExecuteAsync(deviceId, cancellationToken: cancellationToken);
        var explanation = await tutor.ExplainReactionAsync(input, cancellationToken);
        var reaction = ReactionLog.Create(
            CreateEventId(deviceId, input, eventId),
            deviceId,
            profile.Id,
            input.ReactantA,
            input.ReactantB,
            input.ProductFormula,
            input.ReactionEquation,
            input.DeltaEnthalpyKjMol,
            input.ElectronsTransferred,
            input.IsValid,
            input.OutcomeType,
            input.ThermodynamicClassification,
            explanation.Explanation,
            explanation.Source,
            input.Timestamp,
            productDisplayName: input.ProductDisplayName);
        var saved = await history.ExecuteAsync(reaction, cancellationToken);

        return new ReactionExplanationResult(
            explanation.Explanation,
            explanation.Source,
            saved.DuplicateEvent);
    }

    private static string CreateEventId(string deviceId, ReactionExplanationInput input, string? eventId)
    {
        if (eventId is not null)
        {
            return eventId;
        }

        var material = string.Join('|', deviceId, input.ReactantA, input.ReactantB, input.ProductFormula,
            input.ProductDisplayName, input.ReactionEquation, input.IsValid, input.OutcomeType,
            input.DeltaEnthalpyKjMol?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            input.ElectronsTransferred, input.ThermodynamicClassification,
            input.Timestamp.ToUnixTimeMilliseconds());
        return "explain-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    private static string? NormalizeEventId(string? eventId)
    {
        if (eventId is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 128)
        {
            throw new ApplicationException("eventId is invalid.");
        }

        return eventId.Trim();
    }
}
