using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Testcontainers.RabbitMq;
using Xunit;

namespace CharonDataIngestor.IntegrationTests;

public class RabbitMqIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer;
    private IRabbitMqPublisher? _publisher;

    public RabbitMqIntegrationTests()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithPortBinding(5672, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();

        var options = new RabbitMqOptions
        {
            HostName = _rabbitMqContainer.Hostname,
            Port = _rabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest",
            ExchangeName = "metrics",
            QueueName = "metrics.queue",
            RoutingKey = "metrics"
        };

        var optionsMock = Options.Create(options);
        var loggerMock = new Mock<ILogger<RabbitMqPublisher>>();
        
        _publisher = new RabbitMqPublisher(optionsMock, loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        _publisher?.Dispose();
        await _rabbitMqContainer.DisposeAsync();
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishMetric_WhenMetricIsValid()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = "Garage",
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        await _publisher!.PublishAsync(metric);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishMultipleMetrics_WhenMetricsAreValid()
    {
        var metrics = new List<Metric>
        {
            new() { Type = "motion", Name = "Garage", Payload = new Dictionary<string, object> { { "motionDetected", false } } },
            new() { Type = "energy", Name = "Office", Payload = new Dictionary<string, object> { { "energy", 752.91 } } }
        };

        await _publisher!.PublishBatchAsync(metrics);

        await Task.CompletedTask;
    }
}

