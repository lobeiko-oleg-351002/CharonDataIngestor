namespace CharonDataIngestor.Configuration;

public class WeakApiOptions
{
    public const string SectionName = "WeakApi";
    
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "/meters";
    public string ApiKey { get; set; } = "supersecret";
    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
    
    // Circuit Breaker settings
    /// <summary>
    /// Failure threshold as percentage (0-100). Circuit opens when failure rate exceeds this percentage.
    /// Example: 50 = 50% failure rate threshold.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 50;
    
    /// <summary>
    /// Duration in seconds that the circuit breaker stays open before attempting to close.
    /// </summary>
    public int CircuitBreakerDurationOfBreakSeconds { get; set; } = 30;
    
    /// <summary>
    /// Duration in seconds over which failures are sampled to determine if circuit should open.
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 10;
    
    /// <summary>
    /// Minimum number of actions that must occur within the sampling duration for circuit breaker to evaluate.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 2;
    
    // Idempotency settings
    public bool IdempotencyEnabled { get; set; } = true;
    public int IdempotencyKeyTtlSeconds { get; set; } = 300; // 5 minutes
    public int IdempotencyWindowSeconds { get; set; } = 10; // Round timestamp to this window
}

