using Atomiq.Application.Tutor;
using Microsoft.Extensions.Caching.Memory;

namespace Atomiq.Infrastructure.Caching;

public sealed class MemoryReactionExplanationCache(IMemoryCache cache) : IReactionExplanationCache
{
    public TutorExplanation? GetCached(string key)
    {
        return cache.Get<TutorExplanation>(key);
    }

    public void SetCached(string key, TutorExplanation explanation, TimeSpan ttl)
    {
        cache.Set(key, explanation, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1
        });
    }
}
