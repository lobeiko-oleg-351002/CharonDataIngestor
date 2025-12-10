namespace CharonDataIngestor.Configuration;

public class WeakApiOptions
{
    public const string SectionName = "WeakApi";
    
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "/api/metrics";
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
}

