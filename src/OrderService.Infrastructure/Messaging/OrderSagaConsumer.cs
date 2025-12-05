using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Application.Services;
using OrderService.Domain.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Infrastructure.Messaging;

public class OrderSagaConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderSagaConsumer> _logger;
    private readonly IConfiguration _configuration;
    private IConnection _connection;
    private IModel _channel;

    private const string InventoryValidatedQueue = "order.inventory-validated";
    private const string InventoryReservedQueue = "order.inventory-reserved";
    private const string PaymentProcessedQueue = "order.payment-processed";
    
    // Simulation of Payment Service
    private const string ProcessPaymentQueue = "payment.process-payment"; 

    public OrderSagaConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<OrderSagaConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;

        InitializeRabbitMq();
    }

    private void InitializeRabbitMq()
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare Exchanges (we listen to)
        _channel.ExchangeDeclare("domain.catalog.InventoryValidated", ExchangeType.Topic, durable: true);
        _channel.ExchangeDeclare("domain.catalog.InventoryReserved", ExchangeType.Topic, durable: true);
        _channel.ExchangeDeclare("domain.payment.PaymentProcessed", ExchangeType.Topic, durable: true);
        
        // For simulation
        _channel.ExchangeDeclare("domain.order.ProcessPayment", ExchangeType.Topic, durable: true);

        // Declare Queues
        _channel.QueueDeclare(InventoryValidatedQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(InventoryReservedQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(PaymentProcessedQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(ProcessPaymentQueue, durable: true, exclusive: false, autoDelete: false);

        // Bind Queues
        _channel.QueueBind(InventoryValidatedQueue, "domain.catalog.InventoryValidated", "");
        _channel.QueueBind(InventoryReservedQueue, "domain.catalog.InventoryReserved", "");
        _channel.QueueBind(PaymentProcessedQueue, "domain.payment.PaymentProcessed", "");
        _channel.QueueBind(ProcessPaymentQueue, "domain.order.ProcessPayment", "");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            try
            {
                if (ea.Exchange == "domain.catalog.InventoryValidated")
                {
                    await HandleInventoryValidated(message);
                }
                else if (ea.Exchange == "domain.catalog.InventoryReserved")
                {
                    await HandleInventoryReserved(message);
                }
                else if (ea.Exchange == "domain.payment.PaymentProcessed")
                {
                    await HandlePaymentProcessed(message);
                }
                else if (ea.Exchange == "domain.order.ProcessPayment")
                {
                    await HandleProcessPaymentSimulation(message);
                }
                
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from exchange {Exchange}", ea.Exchange);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: InventoryValidatedQueue, autoAck: false, consumer: consumer);
        _channel.BasicConsume(queue: InventoryReservedQueue, autoAck: false, consumer: consumer);
        _channel.BasicConsume(queue: PaymentProcessedQueue, autoAck: false, consumer: consumer);
        _channel.BasicConsume(queue: ProcessPaymentQueue, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task HandleInventoryValidated(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrderSagaOrchestrator>();
        
        var @event = JsonSerializer.Deserialize<InventoryValidatedEvent>(message);
        if (@event == null) return;

        await orchestrator.HandleInventoryValidatedAsync(@event.OrderId, @event.IsValid);
    }

    private async Task HandleInventoryReserved(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrderSagaOrchestrator>();
        
        var @event = JsonSerializer.Deserialize<InventoryReservedEvent>(message);
        if (@event == null) return;

        await orchestrator.HandleInventoryReservedAsync(@event.OrderId, @event.Success);
    }

    private async Task HandlePaymentProcessed(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrderSagaOrchestrator>();
        
        var @event = JsonSerializer.Deserialize<PaymentProcessedEvent>(message);
        if (@event == null) return;

        await orchestrator.HandlePaymentProcessedAsync(@event.OrderId, @event.Success);
    }

    // SIMULATION of Payment Service
    private async Task HandleProcessPaymentSimulation(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        
        var command = JsonSerializer.Deserialize<ProcessPaymentCommand>(message);
        if (command == null) return;

        _logger.LogInformation("Simulating payment processing for Order {OrderId}, Amount: {Amount}", command.OrderId, command.Amount);
        
        // Simulate delay
        await Task.Delay(500);

        var resultEvent = new PaymentProcessedEvent
        {
            OrderId = command.OrderId,
            Success = true, // Always succeed for now
            Reason = "Payment simulated successfully"
        };

        await eventPublisher.PublishAsync("domain.payment.PaymentProcessed", resultEvent);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
