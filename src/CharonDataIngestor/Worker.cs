using CharonDataIngestor.Configuration;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CharonDataIngestor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IWeakApiClient _apiClient;
    private readonly IRabbitMqPublisher _publisher;
    private readonly IMetricValidatorService _validator;
    private readonly IngestionOptions _options;

    public Worker(
        ILogger<Worker> logger,
        IWeakApiClient apiClient,
        IRabbitMqPublisher publisher,
        IMetricValidatorService validator,
        IOptions<IngestionOptions> options)
    {
        _logger = logger;
        _apiClient = apiClient;
        _publisher = publisher;
        _validator = validator;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Ingestor Worker started at: {Time}", DateTimeOffset.Now);

        if (!_options.Enabled)
        {
            _logger.LogWarning("Data ingestion is disabled. Set Ingestion:Enabled to true to enable.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IngestDataAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data ingestion cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Data Ingestor Worker stopped at: {Time}", DateTimeOffset.Now);
    }

    private async Task IngestDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data ingestion cycle at: {Time}", DateTimeOffset.Now);

        try
        {
            var metrics = await _apiClient.FetchMetricsAsync(cancellationToken);
            var metricsList = metrics.ToList();

            if (!metricsList.Any())
            {
                _logger.LogWarning("No metrics fetched from API");
                return;
            }

            _logger.LogInformation("Fetched {Count} metrics from API", metricsList.Count);

            var validationResult = _validator.ValidateBatch(metricsList);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Validation failed for {Count} metrics. Errors: {Errors}",
                    validationResult.Errors.Count(),
                    string.Join("; ", validationResult.Errors));
                
                var validMetrics = metricsList
                    .Where(m => _validator.Validate(m).IsValid)
                    .ToList();
                
                metricsList = validMetrics;
                _logger.LogInformation("Filtered to {Count} valid metrics", metricsList.Count);
            }

            if (metricsList.Any())
            {
                await _publisher.PublishBatchAsync(metricsList, cancellationToken);
                _logger.LogInformation("Successfully published {Count} metrics to RabbitMQ", metricsList.Count);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching metrics. Will retry on next cycle.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Request timeout while fetching metrics. Will retry on next cycle.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during data ingestion");
            throw;
        }
    }
}
