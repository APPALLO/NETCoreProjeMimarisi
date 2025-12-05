using OrderService.Application.DTOs;

namespace OrderService.Application.Services;

public interface IOrderQueryService
{
    Task<OrderDto?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<List<OrderDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrderSagaDto?> GetSagaByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}

public class OrderQueryService : IOrderQueryService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISagaRepository _sagaRepository;

    public OrderQueryService(IOrderRepository orderRepository, ISagaRepository sagaRepository)
    {
        _orderRepository = orderRepository;
        _sagaRepository = sagaRepository;
    }

    public async Task<OrderDto?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        return order == null ? null : new OrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            Status = order.Status.ToString(),
            Items = order.Items,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt
        };
    }

    public async Task<List<OrderDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetByUserIdAsync(userId, cancellationToken);

        return orders.Select(o => new OrderDto
        {
            Id = o.Id,
            UserId = o.UserId,
            Status = o.Status.ToString(),
            Items = o.Items,
            TotalAmount = o.TotalAmount,
            CreatedAt = o.CreatedAt
        }).ToList();
    }

    public async Task<OrderSagaDto?> GetSagaByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId, cancellationToken);

        return saga == null ? null : new OrderSagaDto
        {
            Id = saga.Id,
            OrderId = saga.OrderId,
            Status = saga.Status.ToString(),
            CurrentStep = saga.CurrentStep.ToString(),
            History = saga.History,
            CreatedAt = saga.CreatedAt
        };
    }
}
