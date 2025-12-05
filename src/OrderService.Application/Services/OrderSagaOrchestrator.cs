using Microsoft.Extensions.Logging;
using OrderService.Application.Commands;
using OrderService.Domain.Entities;
using OrderService.Domain.Events;

namespace OrderService.Application.Services;

public interface IOrderSagaOrchestrator
{
    Task<Guid> StartOrderSagaAsync(CreateOrderCommand command, CancellationToken cancellationToken = default);
    Task HandleInventoryValidatedAsync(Guid orderId, bool isValid, CancellationToken cancellationToken = default);
    Task HandleInventoryReservedAsync(Guid orderId, bool success, CancellationToken cancellationToken = default);
    Task HandlePaymentProcessedAsync(Guid orderId, bool success, CancellationToken cancellationToken = default);
}

public interface IEventPublisher
{
    Task PublishAsync<T>(string exchange, T @event, CancellationToken cancellationToken = default);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}

public interface ISagaRepository
{
    Task<OrderSaga?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddAsync(OrderSaga saga, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrderSaga saga, CancellationToken cancellationToken = default);
}

public class OrderSagaOrchestrator : IOrderSagaOrchestrator
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISagaRepository _sagaRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OrderSagaOrchestrator> _logger;

    public OrderSagaOrchestrator(
        IOrderRepository orderRepository,
        ISagaRepository sagaRepository,
        IEventPublisher eventPublisher,
        ILogger<OrderSagaOrchestrator> logger)
    {
        _orderRepository = orderRepository;
        _sagaRepository = sagaRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Guid> StartOrderSagaAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = Order.Create(command.UserId, command.Items);
        var saga = OrderSaga.Start(order.Id);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _sagaRepository.AddAsync(saga, cancellationToken);

        _logger.LogInformation("Order saga started: OrderId={OrderId}, SagaId={SagaId}", order.Id, saga.Id);

        // Step 1: Validate inventory
        var validateEvent = new ValidateInventoryCommand
        {
            OrderId = order.Id,
            Items = command.Items.Select(i => new InventoryItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        await _eventPublisher.PublishAsync("domain.order.ValidateInventory", validateEvent, cancellationToken);

        return order.Id;
    }

    public async Task HandleInventoryValidatedAsync(Guid orderId, bool isValid, CancellationToken cancellationToken = default)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (saga == null) return;

        if (!isValid)
        {
            saga.FailStep(SagaStep.ValidateInventory, "Insufficient inventory");
            await FailOrderAsync(orderId, "Insufficient inventory", cancellationToken);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            return;
        }

        saga.CompleteStep(SagaStep.ValidateInventory, "Inventory validated");
        await _sagaRepository.UpdateAsync(saga, cancellationToken);

        _logger.LogInformation("Inventory validated for order {OrderId}", orderId);

        // Step 2: Reserve inventory
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        var reserveEvent = new ReserveInventoryCommand
        {
            OrderId = orderId,
            Items = order!.Items.Select(i => new InventoryItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        };

        await _eventPublisher.PublishAsync("domain.order.ReserveInventory", reserveEvent, cancellationToken);
    }

    public async Task HandleInventoryReservedAsync(Guid orderId, bool success, CancellationToken cancellationToken = default)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (saga == null) return;

        if (!success)
        {
            saga.FailStep(SagaStep.ReserveInventory, "Failed to reserve inventory");
            saga.StartCompensation();
            await FailOrderAsync(orderId, "Failed to reserve inventory", cancellationToken);
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            return;
        }

        saga.CompleteStep(SagaStep.ReserveInventory, "Inventory reserved");
        await _sagaRepository.UpdateAsync(saga, cancellationToken);

        _logger.LogInformation("Inventory reserved for order {OrderId}", orderId);

        // Step 3: Process payment
        var order = await _orderRepository.GetByIdAsync(orderId);
        var paymentEvent = new ProcessPaymentCommand
        {
            OrderId = orderId,
            UserId = order!.UserId,
            Amount = order.TotalAmount
        };

        await _eventPublisher.PublishAsync("domain.order.ProcessPayment", paymentEvent, cancellationToken);
    }

    public async Task HandlePaymentProcessedAsync(Guid orderId, bool success, CancellationToken cancellationToken = default)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId, cancellationToken);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (saga == null || order == null) return;

        if (!success)
        {
            saga.FailStep(SagaStep.ProcessPayment, "Payment failed");
            saga.StartCompensation();
            await FailOrderAsync(orderId, "Payment failed", cancellationToken);
            
            // Compensate: Release inventory
            var compensateEvent = new ReleaseInventoryCommand { OrderId = orderId };
            await _eventPublisher.PublishAsync("domain.order.ReleaseInventory", compensateEvent, cancellationToken);
            
            await _sagaRepository.UpdateAsync(saga, cancellationToken);
            return;
        }

        saga.CompleteStep(SagaStep.ProcessPayment, "Payment processed");
        order.MarkAsConfirmed();
        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _sagaRepository.UpdateAsync(saga, cancellationToken);

        _logger.LogInformation("Order completed successfully: {OrderId}", orderId);

        var completedEvent = new OrderCompletedEvent
        {
            OrderId = orderId,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount
        };

        await _eventPublisher.PublishAsync("domain.order.OrderCompleted", completedEvent, cancellationToken);
    }

    private async Task FailOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order != null)
        {
            order.MarkAsFailed(reason);
            await _orderRepository.UpdateAsync(order, cancellationToken);
            
            var failedEvent = new OrderFailedEvent
            {
                OrderId = orderId,
                Reason = reason
            };

            await _eventPublisher.PublishAsync("domain.order.OrderFailed", failedEvent, cancellationToken);
        }
    }
}
