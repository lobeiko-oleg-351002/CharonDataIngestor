using CharonDataIngestor.Middleware.Interfaces;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;

namespace CharonDataIngestor.Services.Decorators;

public class RabbitMqPublisherDecorator : IRabbitMqPublisher, IDisposable, IAsyncDisposable
{
    private readonly IRabbitMqPublisher _inner;
    private readonly IExceptionHandlingService _exceptionHandling;

    public RabbitMqPublisherDecorator(
        IRabbitMqPublisher inner,
        IExceptionHandlingService exceptionHandling)
    {
        _inner = inner;
        _exceptionHandling = exceptionHandling;
    }

    public async Task PublishAsync(Metric metric, CancellationToken cancellationToken = default)
    {
        await _exceptionHandling.ExecuteAsync(
            async () => await _inner.PublishAsync(metric, cancellationToken),
            $"{nameof(PublishAsync)} (Type: {metric.Type}, Name: {metric.Name})",
            cancellationToken);
    }

    public async Task PublishBatchAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken = default)
    {
        var metricsList = metrics.ToList();
        var metricsCount = metricsList.Count;
        
        await _exceptionHandling.ExecuteAsync(
            async () => await _inner.PublishBatchAsync(metricsList, cancellationToken),
            $"{nameof(PublishBatchAsync)} (Count: {metricsCount})",
            cancellationToken);
    }

    public void Dispose()
    {
        _inner?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_inner is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            _inner?.Dispose();

        GC.SuppressFinalize(this);
    }
}

