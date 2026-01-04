using CharonDataIngestor.Models;

namespace CharonDataIngestor.Services.Interfaces;

public interface IIdempotencyService
{
    Task<string> GenerateIdempotencyKeyAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<(bool Exists, IEnumerable<Metric>? CachedResult)> TryGetCachedResultAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task CacheResultAsync(string idempotencyKey, IEnumerable<Metric> result, CancellationToken cancellationToken = default);
}

