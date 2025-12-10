namespace CharonDataIngestor.Configuration;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "metrics";
    public string QueueName { get; set; } = "metrics.queue";
    public string RoutingKey { get; set; } = "metrics";
}

