using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using MassTransit;

namespace CharonDataIngestor.Services;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<RabbitMqPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync(Metric metric, CancellationToken cancellationToken = default)
    {
        var message = new MetricMessage
        {
            Type = metric.Type,
            Name = metric.Name,
            Payload = metric.Payload
        };

        await _publishEndpoint.Publish(message, cancellationToken);
    }

    public async Task PublishBatchAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken = default)
    {
        var metricsList = metrics.ToList();
        if (!metricsList.Any())
        {
            return;
        }

        var publishTasks = metricsList.Select(metric =>
        {
            var message = new MetricMessage
            {
                Type = metric.Type,
                Name = metric.Name,
                Payload = metric.Payload
            };
            return _publishEndpoint.Publish(message, cancellationToken);
        });

        await Task.WhenAll(publishTasks);
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

