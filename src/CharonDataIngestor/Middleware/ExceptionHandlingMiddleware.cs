using CharonDataIngestor.Middleware.Interfaces;
using Microsoft.Extensions.Logging;

namespace CharonDataIngestor.Middleware;

public class ExceptionHandlingMiddleware : IExceptionHandlingMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly ILoggingMiddleware _loggingMiddleware;

    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        ILoggingMiddleware loggingMiddleware)
    {
        _logger = logger;
        _loggingMiddleware = loggingMiddleware;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, string operationName, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _loggingMiddleware.LogMethodStart(operationName);

        try
        {
            var result = await action();
            var duration = DateTime.UtcNow - startTime;
            _loggingMiddleware.LogMethodSuccess(operationName, result, duration);
            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _loggingMiddleware.LogMethodFailure(operationName, ex, duration);
            
            if (ShouldRethrow(ex))
            {
                throw;
            }
            
            if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
            {
                throw;
            }
            
            return HandleException(ex, operationName, default(T)!);
        }
    }

    public async Task ExecuteAsync(Func<Task> action, string operationName, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _loggingMiddleware.LogMethodStart(operationName);

        try
        {
            await action();
            var duration = DateTime.UtcNow - startTime;
            _loggingMiddleware.LogMethodSuccess(operationName, duration: duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _loggingMiddleware.LogMethodFailure(operationName, ex, duration);
            
            if (ShouldRethrow(ex))
            {
                throw;
            }
        }
    }

    public T HandleException<T>(Exception exception, string operationName, T defaultValue = default!)
    {
        switch (exception)
        {
            case HttpRequestException httpEx:
                _logger.LogWarning(httpEx, 
                    "HTTP error in {OperationName}. Returning default value.", operationName);
                return defaultValue;

            case TaskCanceledException timeoutEx:
                _logger.LogWarning(timeoutEx, 
                    "Timeout in {OperationName}. Returning default value.", operationName);
                return defaultValue;

            case ArgumentException argEx:
                _logger.LogError(argEx, 
                    "Invalid argument in {OperationName}. Returning default value.", operationName);
                return defaultValue;

            default:
                if (ShouldRethrow(exception))
                {
                    _logger.LogError(exception, 
                        "Unhandled exception in {OperationName}. Rethrowing.", operationName);
                    throw exception;
                }
                
                _logger.LogError(exception, 
                    "Exception in {OperationName}. Returning default value.", operationName);
                return defaultValue;
        }
    }

    public bool ShouldRethrow(Exception exception)
    {
        if (exception is HttpRequestException || exception is TaskCanceledException)
        {
            return false;
        }

        return exception is OutOfMemoryException 
            || exception is StackOverflowException
            || exception is ArgumentNullException;
    }
}

