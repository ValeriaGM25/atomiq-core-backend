using Atomiq.Application.Abstractions;

namespace Atomiq.Application.Profiles;

public sealed class AddXpUseCase(IUserRepository users)
{
    public async Task<AddXpResult?> ExecuteAsync(
        string deviceId,
        string eventId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        if (amount < 0)
        {
            throw new ApplicationException("XP amount cannot be negative.");
        }

        var result = await users.AddXpAsync(deviceId, eventId, amount, cancellationToken);
        if (result.Status == XpUpdateStatus.ProfileNotFound || result.Profile is null)
        {
            return null;
        }

        return new AddXpResult(
            ProfileMappings.ToDto(result.Profile),
            result.Status == XpUpdateStatus.Applied,
            result.Status == XpUpdateStatus.DuplicateEvent);
    }
}
