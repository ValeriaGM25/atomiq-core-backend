using Atomiq.Application.Abstractions;
using Atomiq.Domain;

namespace Atomiq.Application.Reactions;

public sealed class SaveReactionLogUseCase(IReactionHistoryRepository reactions)
{
    public async Task<SaveReactionResult> ExecuteAsync(
        ReactionLog reaction,
        CancellationToken cancellationToken = default)
    {
        var persisted = await reactions.TryCreateAsync(reaction, cancellationToken);
        return new SaveReactionResult(
            ReactionMappings.ToDto(persisted ?? reaction),
            persisted is not null,
            persisted is null);
    }
}
