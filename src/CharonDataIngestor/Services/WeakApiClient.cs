using System.Net.Http.Json;
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
        _options = options.Value;
        _logger = logger;

        _retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: _options.RetryCount,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(
                    _options.RetryDelaySeconds * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}ms. Status: {StatusCode}",
                        retryCount,
                        timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode);
                });
    }

    public async Task<IEnumerable<Metric>> FetchMetricsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _retryPolicy.ExecuteAsync(async () =>
            await _httpClient.GetAsync(_options.Endpoint, cancellationToken));

        response.EnsureSuccessStatusCode();

        var metrics = await response.Content.ReadFromJsonAsync<IEnumerable<Metric>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);

        return metrics ?? Enumerable.Empty<Metric>();
    }
}

