using CharonDataIngestor.Models;
using CharonDataIngestor.Validators;
using FluentAssertions;

namespace CharonDataIngestor.Tests.Validators;

public class MetricValidatorTests
{
    private readonly MetricValidator _validator;

    public MetricValidatorTests()
    {
        _validator = new MetricValidator();
    }

    [Fact]
    public void Validate_ShouldPass_WhenMetricIsValid()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = "Garage",
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ShouldFail_WhenTypeIsNullOrEmpty(string? type)
    {
        var metric = new Metric
        {
            Type = type!,
            Name = "Garage",
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Fact]
    public void Validate_ShouldFail_WhenTypeExceedsMaxLength()
    {
        var metric = new Metric
        {
            Type = new string('a', 101),
            Name = "Garage",
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Type");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_ShouldFail_WhenNameIsNullOrEmpty(string? name)
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = name!,
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = new string('a', 201),
            Payload = new Dictionary<string, object> { { "motionDetected", false } }
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_ShouldFail_WhenPayloadIsNull()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = "Garage",
            Payload = null!
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Payload");
    }

    [Fact]
    public void Validate_ShouldFail_WhenPayloadIsEmpty()
    {
        var metric = new Metric
        {
            Type = "motion",
            Name = "Garage",
            Payload = new Dictionary<string, object>()
        };

        var result = _validator.Validate(metric);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Payload");
    }
}

