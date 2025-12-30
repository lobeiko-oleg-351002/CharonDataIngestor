using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using CharonDbContext.Messages;
using MassTransit;

namespace CharonDataIngestor.Services;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqPublisher(
        IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync(Metric metric, CancellationToken cancellationToken = default)
    {
        var message = new MetricMessage
        {
            Type = metric.Type,
            Name = metric.Name,
            Payload = metric.Payload
        };

        await _publishEndpoint.Publish(message, ctx =>
        {
            ctx.SetRoutingKey("metrics");
        }, cancellationToken);
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
            // Publish to the metrics exchange explicitly
            return _publishEndpoint.Publish(message, ctx =>
            {
                ctx.SetRoutingKey("metrics");
            }, cancellationToken);
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

