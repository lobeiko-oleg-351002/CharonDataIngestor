using CharonDataIngestor.Middleware.Interfaces;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;

namespace CharonDataIngestor.Services.Decorators;

public class WeakApiClientDecorator : IWeakApiClient
{
    private readonly IWeakApiClient _inner;
    private readonly IExceptionHandlingService _exceptionHandling;

    public WeakApiClientDecorator(
        IWeakApiClient inner,
        IExceptionHandlingService exceptionHandling)
    {
        _inner = inner;
        _exceptionHandling = exceptionHandling;
    }

    public async Task<IEnumerable<Metric>> FetchMetricsAsync(CancellationToken cancellationToken = default)
    {
        return await _exceptionHandling.ExecuteAsync(
            async () => await _inner.FetchMetricsAsync(cancellationToken),
            nameof(FetchMetricsAsync),
            cancellationToken) ?? Enumerable.Empty<Metric>();
    }
}

