namespace CharonDataIngestor.Middleware.Interfaces;

public interface IExceptionHandlingService
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, string operationName, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<Task> action, string operationName, CancellationToken cancellationToken = default);
    T HandleException<T>(Exception exception, string operationName, T defaultValue = default!);
    bool ShouldRethrow(Exception exception);
}

