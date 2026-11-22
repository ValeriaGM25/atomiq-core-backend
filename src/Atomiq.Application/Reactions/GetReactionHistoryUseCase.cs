using Atomiq.Application.Abstractions;

namespace Atomiq.Application.Reactions;

public sealed class GetReactionHistoryUseCase(IReactionHistoryRepository reactions)
{
    public async Task<ReactionHistoryPageDto> ExecutePageAsync(
        string deviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ApplicationException("Page must be greater than zero.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ApplicationException("Page size must be between 1 and 100.");
        }

        var skip = checked((page - 1) * pageSize);
        var result = await reactions.GetPageByDeviceIdAsync(
            deviceId,
            skip,
            pageSize,
            cancellationToken: cancellationToken);

        return new ReactionHistoryPageDto(
            result.Items.Select(ReactionMappings.ToDto).ToArray(),
            page,
            pageSize,
            result.HasNextPage);
    }
}
