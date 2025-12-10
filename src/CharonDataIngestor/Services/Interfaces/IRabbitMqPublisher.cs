using CharonDataIngestor.Models;

namespace CharonDataIngestor.Services.Interfaces;

public interface IRabbitMqPublisher : IDisposable, IAsyncDisposable
{
    Task PublishAsync(Metric metric, CancellationToken cancellationToken = default);
    Task PublishBatchAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken = default);
}

