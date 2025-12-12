using CharonDataIngestor.Middleware.Interfaces;
using Microsoft.Extensions.Logging;

namespace CharonDataIngestor.Middleware;

public class LoggingService : ILoggingService
{
    private readonly ILogger<LoggingService> _logger;

    public LoggingService(ILogger<LoggingService> logger)
    {
        _logger = logger;
    }

    public void LogMethodStart(string methodName, object? parameters = null)
    {
        if (parameters != null)
        {
            _logger.LogInformation("Starting {MethodName} with parameters: {@Parameters}", methodName, parameters);
        }
        else
        {
            _logger.LogInformation("Starting {MethodName}", methodName);
        }
    }

    public void LogMethodSuccess(string methodName, object? result = null, TimeSpan? duration = null)
    {
        var message = "Completed {MethodName} successfully";
        var args = new List<object> { methodName };

        if (duration.HasValue)
        {
            message += " in {Duration}ms";
            args.Add(duration.Value.TotalMilliseconds);
        }

        if (result != null)
        {
            message += ". Result: {@Result}";
            args.Add(result);
        }

        _logger.LogInformation(message, args.ToArray());
    }

    public void LogMethodFailure(string methodName, Exception exception, TimeSpan? duration = null)
    {
        var message = "Failed {MethodName}";
        var args = new List<object> { methodName };

        if (duration.HasValue)
        {
            message += " after {Duration}ms";
            args.Add(duration.Value.TotalMilliseconds);
        }

        _logger.LogError(exception, message, args.ToArray());
    }
}

