namespace CharonDataIngestor.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public IEnumerable<string> Errors { get; set; } = Enumerable.Empty<string>();

    public ValidationResult()
    {
    }

    public ValidationResult(bool isValid, IEnumerable<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }
}

