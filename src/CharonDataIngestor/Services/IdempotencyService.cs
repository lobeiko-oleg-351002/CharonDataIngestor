using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CharonDataIngestor.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly IMemoryCache _cache;
    private readonly WeakApiOptions _options;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(
        IMemoryCache cache,
        IOptions<WeakApiOptions> options,
        ILogger<IdempotencyService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> GenerateIdempotencyKeyAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        // Round timestamp to the configured window to ensure idempotency within the time window
        var now = DateTimeOffset.UtcNow;
        var roundedTimestamp = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            now.Minute,
            now.Second / _options.IdempotencyWindowSeconds * _options.IdempotencyWindowSeconds,
            0,
            now.Offset);

        // Generate key: endpoint + rounded timestamp
        var keyData = $"{endpoint}:{roundedTimestamp:yyyy-MM-ddTHH:mm:ss}";
        var keyBytes = Encoding.UTF8.GetBytes(keyData);
        var hashBytes = SHA256.HashData(keyBytes);
        var idempotencyKey = Convert.ToHexString(hashBytes);

        _logger.LogDebug("Generated idempotency key: {Key} for endpoint: {Endpoint}", idempotencyKey, endpoint);
        
        return Task.FromResult(idempotencyKey);
    }

    public Task<(bool Exists, IEnumerable<Metric>? CachedResult)> TryGetCachedResultAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Task.FromResult<(bool, IEnumerable<Metric>?)>((false, null));
        }

        var cacheKey = $"idempotency:{idempotencyKey}";
        
        if (_cache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is IEnumerable<Metric> metrics)
        {
            _logger.LogInformation("Cache hit for idempotency key: {Key}", idempotencyKey);
            return Task.FromResult<(bool, IEnumerable<Metric>?)>((true, metrics));
        }

        _logger.LogDebug("Cache miss for idempotency key: {Key}", idempotencyKey);
        return Task.FromResult<(bool, IEnumerable<Metric>?)>((false, null));
    }

    public Task CacheResultAsync(
        string idempotencyKey,
        IEnumerable<Metric> result,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Task.CompletedTask;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.IdempotencyKeyTtlSeconds),
            SlidingExpiration = null // Use absolute expiration for idempotency
        };

        // Create a copy of the result to avoid reference issues
        var metricsList = result.ToList();
        _cache.Set(cacheKey, metricsList, cacheOptions);

        _logger.LogDebug(
            "Cached result for idempotency key: {Key} with {Count} metrics. TTL: {Ttl} seconds",
            idempotencyKey,
            metricsList.Count,
            _options.IdempotencyKeyTtlSeconds);

        return Task.CompletedTask;
    }
}

