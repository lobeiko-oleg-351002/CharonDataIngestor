using CharonDataIngestor.Models;
using CharonDataIngestor.Services.Interfaces;
using CharonDataIngestor.Validators;
using FluentValidation;

namespace CharonDataIngestor.Services;

public class MetricValidatorService : IMetricValidatorService
{
    private readonly IValidator<Metric> _validator;

    public MetricValidatorService(IValidator<Metric> validator)
    {
        _validator = validator;
    }

    public ValidationResult Validate(Metric metric)
    {
        var result = _validator.Validate(metric);
        return new ValidationResult(result.IsValid, result.Errors.Select(e => e.ErrorMessage));
    }

    public ValidationResult ValidateBatch(IEnumerable<Metric> metrics)
    {
        var allErrors = new List<string>();
        var allValid = true;

        foreach (var metric in metrics)
        {
            var validationResult = Validate(metric);
            if (!validationResult.IsValid)
            {
                allValid = false;
                allErrors.AddRange(validationResult.Errors);
            }
        }

        return new ValidationResult(allValid, allErrors);
    }
}

