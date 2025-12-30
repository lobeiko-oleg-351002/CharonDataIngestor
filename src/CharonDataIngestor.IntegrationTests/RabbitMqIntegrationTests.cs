using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using CharonDataIngestor.Services.Interfaces;
using CharonDbContext.Messages;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;

namespace CharonDataIngestor.IntegrationTests;

public class RabbitMqIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _rabbitMqContainer;
    private IRabbitMqPublisher? _publisher;
    private ServiceProvider? _serviceProvider;

    public RabbitMqIntegrationTests()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("testuser")      // Custom username
            .WithPassword("testpassword")  // Custom password
            .WithPortBinding(5672, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
        
        var port = _rabbitMqContainer.GetMappedPublicPort(5672);
        var hostname = "127.0.0.1"; // Connect via localhost from host machine
        var userName = "testuser";
        var password = "testpassword";

        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = hostname,
            Port = port,
            UserName = userName,
            Password = password,
            ExchangeName = "metrics",
            QueueName = "metrics.queue",
            RoutingKey = "metrics"
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<RabbitMqOptions>(options =>
        {
            options.HostName = rabbitMqOptions.HostName;
            options.Port = rabbitMqOptions.Port;
            options.UserName = rabbitMqOptions.UserName;
            options.Password = rabbitMqOptions.Password;
            options.ExchangeName = rabbitMqOptions.ExchangeName;
            options.QueueName = rabbitMqOptions.QueueName;
            options.RoutingKey = rabbitMqOptions.RoutingKey;
        });
        
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri($"rabbitmq://{rabbitMqOptions.HostName}:{rabbitMqOptions.Port}"), h =>
                {
                    h.Username(rabbitMqOptions.UserName);
                    h.Password(rabbitMqOptions.Password);
                });

                cfg.Message<MetricMessage>(m => m.SetEntityName(rabbitMqOptions.ExchangeName));
                
                cfg.Publish<MetricMessage>(p => p.ExchangeType = "fanout");
            });
        });

        services.AddScoped<RabbitMqPublisher>();
        services.AddScoped<IRabbitMqPublisher>(serviceProvider =>
        {
            return serviceProvider.GetRequiredService<RabbitMqPublisher>();
        });

        _serviceProvider = services.BuildServiceProvider();

        var busControl = _serviceProvider.GetRequiredService<IBusControl>();
        await busControl.StartAsync(CancellationToken.None);

        var publishEndpoint = _serviceProvider.GetRequiredService<IPublishEndpoint>();
        _publisher = new RabbitMqPublisher(publishEndpoint);
    }

    public async Task DisposeAsync()
    {
        _publisher?.Dispose();
        
        if (_serviceProvider != null)
        {
            var busControl = _serviceProvider.GetService<IBusControl>();
            if (busControl != null)
            {
                await busControl.StopAsync(CancellationToken.None);
            }
            
            await _serviceProvider.DisposeAsync();
        }
        
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
    }
}

