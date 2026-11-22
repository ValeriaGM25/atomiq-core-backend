using Atomiq.Application.Reactions;

namespace Atomiq.Api.Contracts;

public sealed record ReactionHistoryResponse(
    IReadOnlyList<ReactionLogDto> Items,
    int Page,
    int PageSize,
    bool HasNextPage);
