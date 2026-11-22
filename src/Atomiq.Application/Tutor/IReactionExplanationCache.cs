namespace Atomiq.Application.Tutor;

public interface IReactionExplanationCache
{
    TutorExplanation? GetCached(string key);

    void SetCached(string key, TutorExplanation explanation, TimeSpan ttl);
}
