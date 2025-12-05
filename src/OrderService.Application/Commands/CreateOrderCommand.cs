using OrderService.Domain.Entities;

namespace OrderService.Application.Commands;

public record CreateOrderCommand
{
    public Guid UserId { get; init; }
    public List<OrderItem> Items { get; init; } = new();
}
