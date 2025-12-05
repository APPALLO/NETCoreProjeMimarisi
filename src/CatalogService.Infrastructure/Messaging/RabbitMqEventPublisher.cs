using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CatalogService.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<T>(string exchange, T @event, CancellationToken cancellationToken = default);
}

public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly ResiliencePipeline _pipeline;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<BrokerUnreachableException>().Handle<OperationInterruptedException>().Handle<Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning("Publish failed. Retrying... Attempt: {Attempt}", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task PublishAsync<T>(string exchange, T @event, CancellationToken cancellationToken = default)
    {
        await _pipeline.ExecuteAsync(async token =>
        {
            using var channel = _connection.CreateModel();
            
            channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true);

            var message = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.MessageId = Guid.NewGuid().ToString();

            channel.BasicPublish(exchange, routingKey: "", basicProperties: properties, body: body);
            
            _logger.LogInformation("Published event to {Exchange}", exchange);
            await Task.CompletedTask;
            
        }, cancellationToken);
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
