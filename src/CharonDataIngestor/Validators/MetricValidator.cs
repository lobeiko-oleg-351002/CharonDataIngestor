using CharonDataIngestor.Models;
using FluentValidation;

namespace CharonDataIngestor.Validators;

public class MetricValidator : AbstractValidator<Metric>
{
    public MetricValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("Metric type is required")
            .MaximumLength(100)
            .WithMessage("Metric type must not exceed 100 characters");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Metric name is required")
            .MaximumLength(200)
            .WithMessage("Metric name must not exceed 200 characters");

        RuleFor(x => x.Payload)
            .NotNull()
            .WithMessage("Metric payload is required")
            .Must(p => p != null && p.Count > 0)
            .WithMessage("Metric payload must contain at least one property");
    }
}

