namespace CharonDataIngestor.Configuration;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";
    
    public int IntervalSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = true;
}

