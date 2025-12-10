using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using CharonDataIngestor.Services.Interfaces;
using CharonDataIngestor.Validators;
using FluentAssertions;
using FluentValidation;

namespace CharonDataIngestor.Tests.Services;

public class MetricValidatorServiceTests
{
    private readonly IMetricValidatorService _validatorService;

    public MetricValidatorServiceTests()
    {
        var validator = new MetricValidator();
        _validatorService = new MetricValidatorService(validator);
    }

    [Fact]
    public void Validate_ShouldReturnValid_WhenMetricIsValid()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = "Garage",
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validatorService.Validate(metric);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenTypeIsEmpty()
    {
        var metric = new Metric
        {
            Type = string.Empty,
            Name = "Garage",
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validatorService.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("type"));
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenNameIsEmpty()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = string.Empty,
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validatorService.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("name"));
    }

    [Fact]
    public void Validate_ShouldReturnInvalid_WhenPayloadIsEmpty()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = "Garage",
            Payload = new Dictionary<string, object>()
        };

        var result = _validatorService.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("payload"));
    }

    [Fact]
    public void ValidateBatch_ShouldReturnValid_WhenAllMetricsAreValid()
    {
        var metrics = new List<Metric>
        {
            new() { Type = "motion", Name = "Garage", Payload = new Dictionary<string, object> { { "motionDetected", false } } },
            new() { Type = "energy", Name = "Office", Payload = new Dictionary<string, object> { { "energy", 752.91 } } }
        };

        var result = _validatorService.ValidateBatch(metrics);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateBatch_ShouldReturnInvalid_WhenSomeMetricsAreInvalid()
    {
        var metrics = new List<Metric>
        {
            new() { Type = "motion", Name = "Garage", Payload = new Dictionary<string, object> { { "motionDetected", false } } },
            new() { Type = string.Empty, Name = "Office", Payload = new Dictionary<string, object> { { "energy", 752.91 } } }
        };

        var result = _validatorService.ValidateBatch(metrics);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}

