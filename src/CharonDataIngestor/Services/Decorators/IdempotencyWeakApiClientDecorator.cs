using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CharonDataIngestor.Services.Decorators;

public class IdempotencyWeakApiClientDecorator : IWeakApiClient
{
    private readonly IWeakApiClient _inner;
    private readonly IIdempotencyService _idempotencyService;
    private readonly WeakApiOptions _options;
    private readonly ILogger<IdempotencyWeakApiClientDecorator> _logger;

    public IdempotencyWeakApiClientDecorator(
        IWeakApiClient inner,
        IIdempotencyService idempotencyService,
        IOptions<WeakApiOptions> options,
        ILogger<IdempotencyWeakApiClientDecorator> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<Metric>> FetchMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IdempotencyEnabled)
        {
            _logger.LogDebug("Idempotency is disabled. Proceeding with direct call.");
            return await _inner.FetchMetricsAsync(cancellationToken);
        }

        // Generate idempotency key based on endpoint
        var idempotencyKey = await _idempotencyService.GenerateIdempotencyKeyAsync(
            _options.Endpoint,
            cancellationToken);

        // Check if we have a cached result
        var (exists, cachedResult) = await _idempotencyService.TryGetCachedResultAsync(
            idempotencyKey,
            cancellationToken);

        if (exists && cachedResult != null)
        {
            _logger.LogInformation(
                "Returning cached result for idempotency key: {Key} with {Count} metrics",
                idempotencyKey,
                cachedResult.Count());
            return cachedResult;
        }

        // Execute the actual request
        _logger.LogDebug("Executing request with idempotency key: {Key}", idempotencyKey);
        var result = await _inner.FetchMetricsAsync(cancellationToken);

        // Cache the result
        await _idempotencyService.CacheResultAsync(idempotencyKey, result, cancellationToken);

        return result;
    }
}

