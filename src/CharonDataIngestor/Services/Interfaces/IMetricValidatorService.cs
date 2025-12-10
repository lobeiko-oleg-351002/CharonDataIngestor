using CharonDataIngestor.Models;

namespace CharonDataIngestor.Services.Interfaces;

public interface IMetricValidatorService
{
    ValidationResult Validate(Metric metric);
    ValidationResult ValidateBatch(IEnumerable<Metric> metrics);
}

