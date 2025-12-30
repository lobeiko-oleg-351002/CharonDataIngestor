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

public class WeakApiClientTests : IDisposable
{
    private readonly Mock<IOptions<WeakApiOptions>> _optionsMock;
    private readonly Mock<ILogger<WeakApiClient>> _loggerMock;
    private readonly WeakApiOptions _options;
    private readonly HttpClient _httpClient;
    private readonly Mock<HttpMessageHandler> _handlerMock;

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

        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);

        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri(_options.BaseUrl)
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldReturnMetrics_WhenApiReturnsSuccess()
    {
        // Arrange
        var expectedMetrics = new List<Metric>
        {
            new() { Type = "motion", Name = "Garage", Payload = new Dictionary<string, object> { { "motionDetected", false } } },
            new() { Type = "energy", Name = "Office", Payload = new Dictionary<string, object> { { "energy", 752.91 } } }
        };

        var jsonResponse = JsonSerializer.Serialize(expectedMetrics);

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        _httpClient.BaseAddress = new Uri("http://localhost:5000/");

        var client = new WeakApiClient(_httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await client.FetchMetricsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var list = result.ToList();

        list[0].Type.Should().Be("motion");
        list[0].Name.Should().Be("Garage");

        list[1].Type.Should().Be("energy");
        list[1].Payload.Should().ContainKey("energy");

        var energyValue = list[1].Payload["energy"] switch
        {
            JsonElement je => je.GetDouble(),
            double d => d,
            _ => 0.0 
        };

        energyValue.Should().BeApproximately(752.91, 0.0001);
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldRetryAndSucceed_WhenApiInitiallyFails()
    {
        // Arrange
        var invocationCount = 0;

        _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)) // 1st call
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)) // 2nd call (1st retry)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new[]
                {
                    new Metric { Type = "motion", Name = "Test", Payload = new Dictionary<string, object>() }
                }), Encoding.UTF8, "application/json")
            });

        var client = new WeakApiClient(_httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await client.FetchMetricsAsync();

        // Assert
        invocationCount = _handlerMock.Invocations.Count;
        invocationCount.Should().Be(3); // Initial + 2 retries = 3 total calls

        result.Should().HaveCount(1);
        result.ElementAt(0).Name.Should().Be("Test");
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldReturnEmptyList_WhenApiReturnsEmptyArray()
    {
        // Arrange
        var emptyJson = JsonSerializer.Serialize(new List<Metric>());

        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(emptyJson, Encoding.UTF8, "application/json")
            });

        var client = new WeakApiClient(_httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await client.FetchMetricsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchMetricsAsync_ShouldReturnEmptyList_AfterAllRetries_WhenApiAlwaysFails()
    {
        // Arrange
        _handlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)); // 3 calls total

        _httpClient.BaseAddress = new Uri("http://localhost:5000/");

        var client = new WeakApiClient(_httpClient, _optionsMock.Object, _loggerMock.Object);

        // Act
        var result = await client.FetchMetricsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Graceful degradation: empty list on failure

        // Verify exactly 3 calls: initial + 2 retries (RetryCount = 2)
        _handlerMock.Invocations.Count.Should().Be(3);
    }
}