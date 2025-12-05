using OrderService.Domain.Entities;

namespace OrderService.Application.DTOs;

public record OrderDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Status { get; init; } = string.Empty;
    public List<OrderItem> Items { get; init; } = new();
    public decimal TotalAmount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record OrderSagaDto
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string CurrentStep { get; init; } = string.Empty;
    public List<SagaStepHistory> History { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}
