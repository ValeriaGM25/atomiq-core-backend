namespace Atomiq.Infrastructure.Mongo;

public interface IMongoIndexReadiness
{
    Task<MongoIndexReadinessResult> GetStatusAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    void Invalidate();
}

public sealed class MongoIndexReadiness(
    MongoIndexInitializer initializer,
    TimeProvider timeProvider) : IMongoIndexReadiness, IDisposable
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(15);
    private readonly SemaphoreSlim gate = new(1, 1);
    private MongoIndexReadinessResult? cachedResult;
    private DateTimeOffset cachedAt;

    public async Task<MongoIndexReadinessResult> GetStatusAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (!forceRefresh
            && cachedResult is not null
            && now - cachedAt < CacheLifetime)
        {
            return cachedResult;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (!forceRefresh
                && cachedResult is not null
                && now - cachedAt < CacheLifetime)
            {
                return cachedResult;
            }

            cachedResult = await initializer.VerifyIndexesAsync(cancellationToken);
            cachedAt = now;
            return cachedResult;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Invalidate()
    {
        cachedResult = null;
        cachedAt = default;
    }

    public void Dispose()
    {
        gate.Dispose();
    }
}
