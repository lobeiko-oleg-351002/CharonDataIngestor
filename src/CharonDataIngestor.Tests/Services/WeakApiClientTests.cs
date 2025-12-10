using CharonDataIngestor.Configuration;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CharonDataIngestor.Tests.Services;

public class WeakApiClientTests
{
    private readonly Mock<IOptions<WeakApiOptions>> _optionsMock;
    private readonly Mock<ILogger<WeakApiClient>> _loggerMock;
    private readonly WeakApiOptions _options;

    public WeakApiClientTests()
    {
        _options = new WeakApiOptions
        {
            BaseUrl = "http://localhost:5000",
            Endpoint = "/api/metrics",
            RetryCount = 2,
            RetryDelaySeconds = 1,
            TimeoutSeconds = 30
        };

        _optionsMock = new Mock<IOptions<WeakApiOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(_options);
        _loggerMock = new Mock<ILogger<WeakApiClient>>();
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldReturnMetrics_WhenApiReturnsSuccess()
    {
        var metrics = new List<Metric>
        {
            new() { Type = "motion", Name = "Garage", Payload = new Dictionary<string, object> { { "motionDetected", false } } },
            new() { Type = "energy", Name = "Office", Payload = new Dictionary<string, object> { { "energy", 752.91 } } }
        };

        var json = JsonSerializer.Serialize(metrics);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new WeakApiClient(httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await client.FetchMetricsAsync();

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.First().Type.Should().Be("motion");
        result.First().Name.Should().Be("Garage");
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldRetry_WhenApiReturnsFailure()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var callCount = 0;
        
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount < 2)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }
                var metrics = new List<Metric>
                {
                    new() { Type = "motion", Name = "Test", Payload = new Dictionary<string, object>() }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(metrics), Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new WeakApiClient(httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await client.FetchMetricsAsync();

        callCount.Should().BeGreaterThan(1);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldReturnEmpty_WhenApiReturnsEmptyArray()
    {
        var json = JsonSerializer.Serialize(new List<Metric>());
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        var httpClient = new HttpClient(handlerMock.Object);
        var client = new WeakApiClient(httpClient, _optionsMock.Object, _loggerMock.Object);

        var result = await client.FetchMetricsAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}

