using System.Text.Json;
using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace CharonDataIngestor.Services;

public class WeakApiClient : IWeakApiClient
{
    private readonly HttpClient _httpClient;
    private readonly WeakApiOptions _options;
    private readonly ILogger<WeakApiClient> _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public WeakApiClient(
        HttpClient httpClient,
        IOptions<WeakApiOptions> options,
        ILogger<WeakApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure retry policy with exponential backoff
        _retryPolicy = HttpPolicyExtensions
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
    }

    public async Task<IEnumerable<Metric>> FetchMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _retryPolicy.ExecuteAsync(
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch metrics from Weak API after retries. Returning empty collection.");
            return Enumerable.Empty<Metric>();
        }
    }
}