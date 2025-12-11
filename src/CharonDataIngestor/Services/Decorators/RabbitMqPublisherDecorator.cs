using CharonDataIngestor.Middleware.Interfaces;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;

namespace CharonDataIngestor.Services.Decorators;

public class RabbitMqPublisherDecorator : IRabbitMqPublisher
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_inner != null)
        {
            await _inner.DisposeAsync();
        }
    }
}

