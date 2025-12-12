using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using CharonDataIngestor.Services.Interfaces;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

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
            .WithUsername("guest")
            .WithPassword("guest")
            .WithPortBinding(5672, true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _rabbitMqContainer.StartAsync();
        
        var port = _rabbitMqContainer.GetMappedPublicPort(5672);
        var hostName = "localhost";
        var userName = "guest";
        var password = "guest";
        
        var maxRetries = 30;
        var retryDelay = TimeSpan.FromMilliseconds(500);
        var connected = false;
        
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    Port = port,
                    UserName = userName,
                    Password = password,
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(2)
                };
                
                using var connection = await factory.CreateConnectionAsync();
                connected = connection.IsOpen;
                await connection.CloseAsync();
                break;
            }
            catch (Exception) when (i < maxRetries - 1)
            {
                await Task.Delay(retryDelay);
            }
        }
        
        if (!connected)
        {
            throw new InvalidOperationException($"Failed to connect to RabbitMQ after {maxRetries} attempts");
        }

        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = hostName,
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
        var logger = _serviceProvider.GetRequiredService<ILogger<RabbitMqPublisher>>();
        
        _publisher = new RabbitMqPublisher(publishEndpoint, logger);
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

