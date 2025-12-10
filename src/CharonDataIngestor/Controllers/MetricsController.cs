using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CharonDataIngestor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IWeakApiClient _apiClient;
    private readonly IRabbitMqPublisher _publisher;
    private readonly IMetricValidatorService _validator;

    public MetricsController(
        IWeakApiClient apiClient,
        IRabbitMqPublisher publisher,
        IMetricValidatorService validator)
    {
        _apiClient = apiClient;
        _publisher = publisher;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Metric>>> GetMetrics(CancellationToken cancellationToken = default)
    {
        var metrics = await _apiClient.FetchMetricsAsync(cancellationToken);
        return Ok(metrics);
    }

    [HttpPost]
    public async Task<ActionResult> PublishMetric([FromBody] Metric metric, CancellationToken cancellationToken = default)
    {
        var validationResult = _validator.Validate(metric);
        if (!validationResult.IsValid)
        {
            return BadRequest(new { Errors = validationResult.Errors });
        }

        await _publisher.PublishAsync(metric, cancellationToken);
        return Ok(new { Message = "Metric published successfully" });
    }

    [HttpPost("batch")]
    public async Task<ActionResult> PublishMetrics([FromBody] IEnumerable<Metric> metrics, CancellationToken cancellationToken = default)
    {
        var metricsList = metrics.ToList();
        var validationResult = _validator.ValidateBatch(metricsList);
        
        if (!validationResult.IsValid)
        {
            return BadRequest(new { Errors = validationResult.Errors });
        }

        await _publisher.PublishBatchAsync(metricsList, cancellationToken);
        return Ok(new { Message = $"{metricsList.Count} metrics published successfully" });
    }
}

