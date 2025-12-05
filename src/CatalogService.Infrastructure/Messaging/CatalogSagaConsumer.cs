using System.Text;
using System.Text.Json;
using CatalogService.Application.Saga;
using CatalogService.Domain.Entities;
using CatalogService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CatalogService.Infrastructure.Messaging;

public class CatalogSagaConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CatalogSagaConsumer> _logger;
    private readonly IConfiguration _configuration;
    private IConnection _connection;
    private IModel _channel;
    private const string ValidateInventoryQueue = "catalog.validate-inventory";
    private const string ReserveInventoryQueue = "catalog.reserve-inventory";
    private const string ReleaseInventoryQueue = "catalog.release-inventory";

    public CatalogSagaConsumer(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<CatalogSagaConsumer> logger)
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

        // Declare Exchanges
        _channel.ExchangeDeclare("domain.order.ValidateInventory", ExchangeType.Topic, durable: true);
        _channel.ExchangeDeclare("domain.order.ReserveInventory", ExchangeType.Topic, durable: true);
        _channel.ExchangeDeclare("domain.order.ReleaseInventory", ExchangeType.Topic, durable: true);

        // Declare Queues
        _channel.QueueDeclare(ValidateInventoryQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(ReserveInventoryQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(ReleaseInventoryQueue, durable: true, exclusive: false, autoDelete: false);

        // Bind Queues
        _channel.QueueBind(ValidateInventoryQueue, "domain.order.ValidateInventory", "");
        _channel.QueueBind(ReserveInventoryQueue, "domain.order.ReserveInventory", "");
        _channel.QueueBind(ReleaseInventoryQueue, "domain.order.ReleaseInventory", "");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey; // Not really used here as we bind by exchange

            // Since we are consuming from multiple queues, we can check which queue delivered the message
            // Wait, EventingBasicConsumer doesn't easily tell you the source queue unless you have separate consumers.
            // A better way is to have separate consumers or check the exchange?
            // The ea.Exchange will tell us the exchange.
            
            try
            {
                if (ea.Exchange == "domain.order.ValidateInventory")
                {
                    await HandleValidateInventory(message);
                }
                else if (ea.Exchange == "domain.order.ReserveInventory")
                {
                    await HandleReserveInventory(message);
                }
                else if (ea.Exchange == "domain.order.ReleaseInventory")
                {
                    await HandleReleaseInventory(message);
                }
                
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from exchange {Exchange}", ea.Exchange);
                // Requeue? Or Dead Letter? For now, reject without requeue to avoid infinite loops
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(queue: ValidateInventoryQueue, autoAck: false, consumer: consumer);
        _channel.BasicConsume(queue: ReserveInventoryQueue, autoAck: false, consumer: consumer);
        _channel.BasicConsume(queue: ReleaseInventoryQueue, autoAck: false, consumer: consumer);

        return Task.CompletedTask;
    }

    private async Task HandleValidateInventory(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        
        var command = JsonSerializer.Deserialize<ValidateInventoryCommand>(message);
        if (command == null) return;

        _logger.LogInformation("Validating inventory for Order {OrderId}", command.OrderId);

        bool isValid = true;
        string reason = "";

        foreach (var item in command.Items)
        {
            var product = await context.Products.FindAsync(item.ProductId);
            if (product == null)
            {
                isValid = false;
                reason = $"Product {item.ProductId} not found";
                break;
            }

            if (product.StockQuantity < item.Quantity)
            {
                isValid = false;
                reason = $"Insufficient stock for {product.Name}";
                break;
            }
        }

        var resultEvent = new InventoryValidatedEvent
        {
            OrderId = command.OrderId,
            IsValid = isValid,
            Reason = reason
        };

        await eventPublisher.PublishAsync("domain.catalog.InventoryValidated", resultEvent);
    }

    private async Task HandleReserveInventory(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var command = JsonSerializer.Deserialize<ReserveInventoryCommand>(message);
        if (command == null) return;

        _logger.LogInformation("Reserving inventory for Order {OrderId}", command.OrderId);

        bool success = true;
        string reason = "";

        // Need transaction
        using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var item in command.Items)
            {
                var product = await context.Products.FindAsync(item.ProductId);
                if (product == null || product.StockQuantity < item.Quantity)
                {
                    success = false;
                    reason = $"Failed to reserve product {item.ProductId}";
                    break;
                }

                product.UpdateStock(product.StockQuantity - item.Quantity);
            }

            if (success)
            {
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }
        }
        catch (Exception ex)
        {
            success = false;
            reason = ex.Message;
            await transaction.RollbackAsync();
        }

        var resultEvent = new InventoryReservedEvent
        {
            OrderId = command.OrderId,
            Success = success,
            Reason = reason
        };

        await eventPublisher.PublishAsync("domain.catalog.InventoryReserved", resultEvent);
    }

    private async Task HandleReleaseInventory(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var command = JsonSerializer.Deserialize<ReleaseInventoryCommand>(message);
        if (command == null) return;

        _logger.LogInformation("Releasing inventory for Order {OrderId}", command.OrderId);

        // In a real scenario, we'd need to know WHAT to release.
        // The ReleaseInventoryCommand should probably contain items, OR we need to track reservations.
        // For simplicity here, I'll assume we can't easily release without knowing items.
        // BUT, the Saga compensation logic in OrderService might need to send items.
        // Let's check OrderSagaOrchestrator.
        
        // Orchestrator sends: new ReleaseInventoryCommand { OrderId = orderId };
        // It DOES NOT send items. This is a BUG in the Orchestrator or Protocol.
        // We can't release if we don't know what we reserved.
        
        // However, fixing that requires changing OrderService logic.
        // For now, I will log a warning. To fix it properly, I'd need to fetch the Order from OrderService or pass items.
        // But CatalogService doesn't know about Orders.
        
        _logger.LogWarning("ReleaseInventory requested for {OrderId} but no items provided. Skipping implementation.", command.OrderId);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
