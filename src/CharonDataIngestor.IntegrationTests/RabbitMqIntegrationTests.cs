using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using CharonDataIngestor.Services.Interfaces;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;
using Xunit;

namespace CharonDataIngestor.IntegrationTests;

public class RabbitMqIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer;
    private IRabbitMqPublisher? _publisher;
    private ServiceProvider? _serviceProvider;
    private IBusControl? _busControl;

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

        var port = _rabbitMqContainer.GetMappedPublicPort(5672);
        var hostname = _rabbitMqContainer.Hostname;

        var services = new ServiceCollection();
        
        services.AddLogging();
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri($"rabbitmq://{hostname}:{port}"), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });
            });
        });

        _serviceProvider = services.BuildServiceProvider();
        _busControl = _serviceProvider.GetRequiredService<IBusControl>();
        await _busControl.StartAsync();

        var publishEndpoint = _serviceProvider.GetRequiredService<IPublishEndpoint>();
        var logger = _serviceProvider.GetRequiredService<ILogger<RabbitMqPublisher>>();
        
        _publisher = new RabbitMqPublisher(publishEndpoint, logger);
    }

    public async Task DisposeAsync()
    {
        if (_busControl != null)
        {
            await _busControl.StopAsync();
        }
        
        _publisher?.Dispose();
        _serviceProvider?.Dispose();
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

