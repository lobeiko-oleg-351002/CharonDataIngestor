namespace CharonDataIngestor.Middleware.Interfaces;

public interface ILoggingMiddleware
{
    void LogMethodStart(string methodName, object? parameters = null);
    void LogMethodSuccess(string methodName, object? result = null, TimeSpan? duration = null);
    void LogMethodFailure(string methodName, Exception exception, TimeSpan? duration = null);
}

