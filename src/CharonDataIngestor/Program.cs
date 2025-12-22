using CharonDataIngestor;
using CharonDataIngestor.Configuration;
using CharonDataIngestor.Middleware;
using CharonDataIngestor.Middleware.Interfaces;
using CharonDataIngestor.Models;
using CharonDataIngestor.Services;
using CharonDataIngestor.Services.Decorators;
using CharonDataIngestor.Services.Interfaces;
using CharonDataIngestor.Validators;
using CharonDbContext.Messages;
using FluentValidation;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CharonDataIngestor")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/charon-data-ingestor-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Charon Data Ingestor");

    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();
    
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.Configure<WeakApiOptions>(
        builder.Configuration.GetSection(WeakApiOptions.SectionName));
    builder.Services.Configure<RabbitMqOptions>(
        builder.Configuration.GetSection(RabbitMqOptions.SectionName));
    builder.Services.Configure<IngestionOptions>(
        builder.Configuration.GetSection(IngestionOptions.SectionName));

    builder.Services.AddHttpClient<WeakApiClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<WeakApiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    });
    
    builder.Services.AddScoped<IWeakApiClient>(serviceProvider =>
    {
        var inner = serviceProvider.GetRequiredService<WeakApiClient>();
        var exceptionHandling = serviceProvider.GetRequiredService<IExceptionHandlingService>();
        return new WeakApiClientDecorator(inner, exceptionHandling);
    });

    builder.Services.AddValidatorsFromAssemblyContaining<MetricValidator>();
    builder.Services.AddScoped<IMetricValidatorService, MetricValidatorService>();

    builder.Services.AddScoped<ILoggingService, LoggingService>();
    builder.Services.AddScoped<IExceptionHandlingService, ExceptionHandlingService>();

    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            var options = builder.Configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>();
            if (options != null)
            {
                cfg.Host(new Uri($"rabbitmq://{options.HostName}:{options.Port}"), h =>
                {
                    h.Username(options.UserName);
                    h.Password(options.Password);
                });

                // Use CharonDbContext.Messages.MetricMessage - shared contract!
                Log.Information("===> Using CharonDbContext.Messages.MetricMessage for publishing");

                // Publish to fanout exchange
                cfg.Publish<MetricMessage>(p => 
                {
                    p.ExchangeType = "fanout";
                });
            }
        });
    });

    builder.Services.AddScoped<RabbitMqPublisher>();
    builder.Services.AddScoped<IRabbitMqPublisher>(serviceProvider =>
    {
        var inner = serviceProvider.GetRequiredService<RabbitMqPublisher>();
        var exceptionHandling = serviceProvider.GetRequiredService<IExceptionHandlingService>();
        return new RabbitMqPublisherDecorator(inner, exceptionHandling);
    });

    builder.Services.AddHostedService<Worker>();

    var app = builder.Build();
    
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Charon Data Ingestor API v1");
            c.RoutePrefix = string.Empty;
        });
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    
    Log.Information("Charon Data Ingestor started successfully");
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Charon Data Ingestor terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
