namespace OrderService.Domain.Events;

public record OrderCreatedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int Version { get; init; } = 1;
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal TotalAmount { get; init; }
}

public record OrderCompletedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int Version { get; init; } = 1;
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal TotalAmount { get; init; }
}

public record OrderFailedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int Version { get; init; } = 1;
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record ValidateInventoryCommand
{
    public Guid OrderId { get; init; }
    public List<InventoryItem> Items { get; init; } = new();
}

public record ReserveInventoryCommand
{
    public Guid OrderId { get; init; }
    public List<InventoryItem> Items { get; init; } = new();
}

public record ReleaseInventoryCommand
{
    public Guid OrderId { get; init; }
}

public record ProcessPaymentCommand
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public decimal Amount { get; init; }
}

public record InventoryItem
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
}
