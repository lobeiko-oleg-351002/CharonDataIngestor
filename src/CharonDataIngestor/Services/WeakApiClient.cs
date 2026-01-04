using System.Text.Json;
using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;

namespace CharonDataIngestor.Services;

public class WeakApiClient : IWeakApiClient
{
    private readonly HttpClient _httpClient;
    private readonly WeakApiOptions _options;
    private readonly ILogger<WeakApiClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _resiliencePolicy;

    public WeakApiClient(
        HttpClient httpClient,
        IOptions<WeakApiOptions> options,
        ILogger<WeakApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure Circuit Breaker
        // failureThreshold is a percentage (0.0 to 1.0), e.g., 0.5 = 50% failure rate
        var circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(msg => !msg.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: _options.CircuitBreakerFailureThreshold / 100.0,
                samplingDuration: TimeSpan.FromSeconds(_options.CircuitBreakerSamplingDurationSeconds),
                minimumThroughput: _options.CircuitBreakerMinimumThroughput,
                durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreakerDurationOfBreakSeconds),
                onBreak: (result, duration) =>
                {
                    var reason = result?.Exception?.Message 
                        ?? result?.Result?.StatusCode.ToString() 
                        ?? "Unknown";
                    _logger.LogWarning(
                        "Circuit breaker opened for {Duration} seconds. Reason: {Reason}",
                        duration.TotalSeconds,
                        reason);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset. Requests will be allowed again.");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open. Testing if service is available.");
                });

        // Configure retry policy with exponential backoff
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: _options.RetryCount,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(_options.RetryDelaySeconds * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "Unknown";
                    _logger.LogWarning(
                        "WeakApiClient retry {RetryAttempt}/{RetryCount} after {Delay}ms due to {StatusCode}",
                        retryAttempt,
                        _options.RetryCount,
                        timespan.TotalMilliseconds,
                        statusCode);
                });

        // Combine policies: Circuit Breaker wraps Retry
        _resiliencePolicy = Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
    }

    public async Task<IEnumerable<Metric>> FetchMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _resiliencePolicy.ExecuteAsync(
                () => _httpClient.GetAsync(_options.Endpoint, cancellationToken));

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return Enumerable.Empty<Metric>();
            }

            var metrics = await response.Content.ReadFromJsonAsync<List<Metric>>(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                cancellationToken);

            return metrics ?? Enumerable.Empty<Metric>();
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open. Weak API is unavailable. Returning empty collection.");
            return Enumerable.Empty<Metric>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch metrics from Weak API after retries. Returning empty collection.");
            return Enumerable.Empty<Metric>();
        }
    }
}