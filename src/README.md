# Charon Data Ingestor

A microservice that fetches data from an unstable external API and publishes it to RabbitMQ for further processing.

## Features

- ✅ Fetches metrics from WeakApp API with retry logic (Polly)
- ✅ Publishes metrics to RabbitMQ message queue
- ✅ FluentValidation for data validation
- ✅ Serilog for structured logging
- ✅ Comprehensive error handling
- ✅ Unit and Integration tests
- ✅ Docker support
- ✅ CI/CD with GitHub Actions

## Architecture

The Data Ingestor service:
1. Periodically fetches metrics from the WeakApp API
2. Validates the metrics using FluentValidation
3. Publishes valid metrics to RabbitMQ for consumption by the Data Processor service

## Configuration

The service can be configured via `appsettings.json` or environment variables:

### WeakApi Configuration
```json
{
  "WeakApi": {
    "BaseUrl": "http://weakapp:5000",
    "Endpoint": "/api/metrics",
    "RetryCount": 3,
    "RetryDelaySeconds": 2,
    "TimeoutSeconds": 30
  }
}
```

### RabbitMQ Configuration
```json
{
  "RabbitMq": {
    "HostName": "rabbitmq",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "ExchangeName": "metrics",
    "QueueName": "metrics.queue",
    "RoutingKey": "metrics"
  }
}
```

### Ingestion Configuration
```json
{
  "Ingestion": {
    "IntervalSeconds": 10,
    "Enabled": true
  }
}
```

## Running Locally

### Prerequisites
- .NET 9.0 SDK
- Docker and Docker Compose (for full stack)
- RabbitMQ (or use Docker Compose)

### Development

1. Clone the repository
2. Restore dependencies:
   ```bash
   dotnet restore
   ```
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Run tests:
   ```bash
   dotnet test
   ```
5. Run the service:
   ```bash
   dotnet run --project CharonDataIngestor/CharonDataIngestor.csproj
   ```

### Docker Compose

To run the entire stack (WeakApp, RabbitMQ, and Data Ingestor):

```bash
docker-compose up -d
```

The service will:
- Wait for RabbitMQ and WeakApp to be healthy
- Start ingesting data every 10 seconds
- Publish metrics to RabbitMQ

## Testing

### Unit Tests
```bash
dotnet test CharonDataIngestor.Tests/CharonDataIngestor.Tests.csproj
```

### Integration Tests
```bash
dotnet test CharonDataIngestor.IntegrationTests/CharonDataIngestor.IntegrationTests.csproj
```

## Logging

Logs are written to:
- Console (structured JSON)
- File: `logs/charon-data-ingestor-{date}.log`

Log levels can be configured in `appsettings.json`.

## CI/CD

The GitHub Actions workflow:
1. Builds the solution
2. Runs unit tests with code coverage
3. Checks code formatting
4. Builds and pushes Docker image (on main branch)

## Project Structure

```
CharonDataIngestor/
├── CharonDataIngestor/          # Main service project
│   ├── Configuration/           # Configuration classes
│   ├── Models/                  # Data models
│   ├── Services/                # Business logic services
│   ├── Validators/              # FluentValidation validators
│   ├── Program.cs               # Application entry point
│   └── Worker.cs                # Background worker
├── CharonDataIngestor.Tests/    # Unit tests
├── CharonDataIngestor.IntegrationTests/  # Integration tests
├── Dockerfile                   # Docker image definition
├── docker-compose.yml           # Docker Compose configuration
└── .github/workflows/           # CI/CD pipelines
```

## Technologies Used

- .NET 9.0
- Polly (Retry policies)
- RabbitMQ.Client
- FluentValidation
- Serilog
- xUnit
- Moq
- FluentAssertions
- Testcontainers (for integration tests)

## License

MIT

