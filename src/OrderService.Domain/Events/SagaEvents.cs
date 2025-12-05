namespace OrderService.Domain.Events;

public record InventoryValidatedEvent
{
    public Guid OrderId { get; init; }
    public bool IsValid { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record InventoryReservedEvent
{
    public Guid OrderId { get; init; }
    public bool Success { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record PaymentProcessedEvent
{
    public Guid OrderId { get; init; }
    public bool Success { get; init; }
    public string Reason { get; init; } = string.Empty;
}
