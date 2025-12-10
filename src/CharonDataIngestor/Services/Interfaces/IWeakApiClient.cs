using CharonDataIngestor.Models;

namespace CharonDataIngestor.Services.Interfaces;

public interface IWeakApiClient
{
    Task<IEnumerable<Metric>> FetchMetricsAsync(CancellationToken cancellationToken = default);
}

