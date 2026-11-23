namespace Atomiq.Application.Tutor;

public interface IAiTutorService
{
    Task<TutorExplanation> ExplainReactionAsync(
        ReactionExplanationInput input,
        CancellationToken cancellationToken = default);
}
